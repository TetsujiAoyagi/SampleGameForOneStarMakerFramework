#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OneStarMaker.Foundation.Logging;
using ZLogger;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class AppLoggerTailFailureTests
    {
        private const int Limit = 65533;
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

        [Test]
        public void Factory_TailSerializationFailure_RecordsBothProviders()
        {
            // This directory is intentionally outside the checkout and survives every outcome.
            var result = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "osm-tail-evidence-" + Guid.NewGuid().ToString("N"))).FullName;
            TestContext.Progress.WriteLine("Tail failure evidence: " + result);
            var facts = new List<string>();
            try
            {
                var headRecorded = RecordIdentity(facts);
                var control = Run(result, "control", false, facts);
                var failure = Run(result, "failure", true, facts);
                var newline = Encoding.UTF8.GetBytes(Environment.NewLine);
                var validControl = control.Completed && control.Rolling != null && control.Realtime != null
                    && IsCompleteA(control.Rolling, newline) && IsCompleteA(control.Realtime, newline);
                var rollingLength = control.Rolling == null ? -1 : control.Rolling.Length - newline.Length;
                var realtimeLength = control.Realtime == null ? -1 : control.Realtime.Length - newline.Length;
                facts.Add($"control-valid={validControl}; rolling-copy={rollingLength}; realtime-copy={realtimeLength}");
                // Scopes are disabled and the template, category and probe are identical.
                // Reserve space for any variable-width timestamp representation between runs.
                var established = headRecorded && validControl && rollingLength <= Limit - 1024 && realtimeLength <= Limit - 1024
                    && failure.Completed && failure.ABoth && failure.BLogged && failure.BBoth && failure.BFailed
                    && failure.WritesAtB == 0 && failure.SpanWritesAtB == 0 && failure.NoticesRead && failure.Rolling != null && failure.Realtime != null
                    && failure.IdentityMatches;
                facts.Add("conditions-established=" + established);
                if (established)
                {
                    facts.Add("rolling-class=" + Classify(failure.Rolling!, newline));
                    facts.Add("realtime-class=" + Classify(failure.Realtime!, newline));
                    facts.Add("rolling-markers=" + Markers(failure.Rolling!));
                    facts.Add("realtime-markers=" + Markers(failure.Realtime!));
                    facts.Add("rolling-notice=" + NoticeState(failure.Notices, "rolling-json"));
                    facts.Add("realtime-notice=" + NoticeState(failure.Notices, "realtime-json"));
                }
                Assert.IsTrue(established, "Observation conditions or evidence collection failed; see " + result);
            }
            finally
            {
                File.WriteAllLines(Path.Combine(result, "observations.txt"), facts);
            }
        }

        private static Observation Run(string result, string name, bool failing, List<string> facts)
        {
            var root = Directory.CreateDirectory(Path.Combine(result, name + "-source")).FullName;
            var saved = Directory.CreateDirectory(Path.Combine(result, name)).FullName;
            var a = new Gate();
            var b = new Gate();
            var allowB = new ManualResetEventSlim();
            var bReturned = new ManualResetEventSlim();
            var finished = new ManualResetEventSlim();
            var watchdogFinished = new ManualResetEventSlim();
            var stream = new ObservedStream();
            var observation = new Observation();
            AppLoggerFactory? factory = null;
            Thread? producer = null;
            Thread? disposer = null;
            Exception? producerError = null;
            Exception? disposeError = null;
            var executionCompleted = false;
            var watchdogStopped = false;
            var watchdog = new Thread(() =>
            {
                if (!finished.Wait(Deadline))
                {
                    observation.TimedOut = true;
                    a.Release.Set(); b.Release.Set(); allowB.Set();
                }
                watchdogFinished.Set();
            }) { IsBackground = true, Name = "tail-watchdog" };
            watchdog.Start();
            try
            {
                factory = new AppLoggerFactory(root, stream, rollingSizeKb: 16384, realtimeLogFormat: RealtimeLogFormat.Json);
                var logger = factory.CreateLogger("TailFailure");
                producer = new Thread(() =>
                {
                    try
                    {
                        logger.LogInformation("{Id} {Probe}", "A", new Probe(failing ? a : null, false));
                        if (failing)
                        {
                            allowB.Wait();
                            logger.LogInformation("{Id} {Probe}", "B", new Probe(b, true));
                            observation.BLogged = true;
                        }
                    }
                    catch (Exception e) { producerError = e; }
                    finally { bReturned.Set(); }
                }) { IsBackground = true, Name = "tail-producer" };
                producer.Start();
                if (failing)
                {
                    observation.ABoth = a.Both.Wait(Deadline) && a.Simultaneous;
                    allowB.Set();
                    if (bReturned.Wait(Deadline) && observation.BLogged)
                    {
                        a.Release.Set();
                        observation.BBoth = b.Both.Wait(Deadline) && b.Simultaneous;
                        observation.WritesAtB = stream.ByteArrayWrites;
                        observation.SpanWritesAtB = stream.SpanWrites;
                    }
                    b.Release.Set();
                    observation.BFailed = b.BothFailed.Wait(Deadline);
                }
                if (!producer.Join(Deadline)) facts.Add(name + ": producer did not finish");
                if (!producer.IsAlive)
                {
                    disposer = new Thread(() => { try { factory.Dispose(); } catch (Exception e) { disposeError = e; } })
                        { IsBackground = true, Name = "tail-disposer" };
                    disposer.Start();
                    if (!disposer.Join(Deadline)) facts.Add(name + ": dispose did not finish");
                }
                executionCompleted = !producer.IsAlive && disposer != null
                    && !disposer.IsAlive && producerError == null && disposeError == null;
            }
            catch (Exception e) { facts.Add(name + ": exception=" + e); }
            finally
            {
                a.Release.Set(); b.Release.Set(); allowB.Set();
                finished.Set();
                watchdogStopped = watchdogFinished.Wait(Deadline);
                if (producer != null) producer.Join(Deadline);
                if (disposer != null) disposer.Join(Deadline);
                facts.Add($"{name}: timeout={observation.TimedOut}; watchdog-stopped={watchdogStopped}; producer-alive={producer?.IsAlive}; dispose-alive={disposer?.IsAlive}; producer-error={producerError}; dispose-error={disposeError}");
                facts.Add($"{name}: A-both={observation.ABoth}; A={a.Describe()}; B-both={observation.BBoth}; B-failed={observation.BFailed}; B={b.Describe()}; B-returned={observation.BLogged}; writes-at-B={observation.WritesAtB}; span-writes-at-B={observation.SpanWritesAtB}; total-writes={stream.ByteArrayWrites}; total-span-writes={stream.SpanWrites}");
                observation.IdentityMatches = IdentityMatches(facts, name);
                try
                {
                    var bytes = stream.Snapshot();
                    SaveBytes(saved, "realtime.bin", bytes, facts);
                    observation.Realtime = bytes;
                }
                catch (Exception e) { facts.Add(name + ": realtime-read-error=" + e); }
                try
                {
                    var files = Directory.GetFiles(root, "*.log");
                    // More than one file cannot be ordered from this observation alone.
                    facts.Add(name + ": rolling-files=" + files.Length);
                    foreach (var file in files) File.Copy(file, Path.Combine(saved, Path.GetFileName(file)), true);
                    if (files.Length <= 1)
                    {
                        var bytes = files.Length == 0 ? Array.Empty<byte>() : ReadShared(files[0]);
                        SaveBytes(saved, "rolling.bin", bytes, facts);
                        observation.Rolling = bytes;
                    }
                }
                catch (Exception e) { facts.Add(name + ": rolling-read-error=" + e); }
                try
                {
                    var files = Directory.GetFiles(root, "logging-errors_*.txt");
                    var lines = new List<string>();
                    foreach (var file in files)
                    {
                        var bytes = ReadShared(file);
                        SaveBytes(saved, Path.GetFileName(file), bytes, facts);
                        lines.AddRange(Encoding.UTF8.GetString(bytes).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                    observation.Notices = lines.ToArray();
                    observation.NoticesRead = true;
                    facts.Add(name + ": notice-files=" + files.Length);
                }
                catch (Exception e) { facts.Add(name + ": notice-read-error=" + e); }
            }
            // The watchdog can time out between the execution check and finished.Set().
            // Read its final state only after its completion signal has been observed.
            observation.Completed = executionCompleted && watchdogStopped && !observation.TimedOut;
            return observation;
        }

        private static bool IsCompleteA(byte[] bytes, byte[] newline)
        {
            if (bytes.Length <= newline.Length + 1 || bytes[0] != (byte)'{' || bytes[bytes.Length - newline.Length - 1] != (byte)'}'
                || !bytes.Skip(bytes.Length - newline.Length).SequenceEqual(newline)) return false;
            try
            {
                using var doc = JsonDocument.Parse(new ReadOnlyMemory<byte>(bytes, 0, bytes.Length - newline.Length));
                var root = doc.RootElement;
                return root.GetProperty("Id").GetString() == "A"
                    && root.GetProperty("Probe").GetProperty("Value").GetInt32() == 17;
            }
            catch (Exception) { return false; }
        }

        private static string Classify(byte[] bytes, byte[] newline)
            => bytes.Length == 0 ? "zero-bytes" : IsCompleteA(bytes, newline) ? "complete-A" : "residual";
        private static string Markers(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return "A-id=" + text.Contains("\"Id\":\"A\"") + "; B-id=" + text.Contains("\"Id\":\"B\"");
        }
        private static string NoticeState(string[] lines, string provider)
            => lines.Any(x => x.Contains("|" + provider + "|")) ? "lines-present" : "no-lines";
        private static byte[] ReadShared(string path)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = new MemoryStream(); input.CopyTo(output); return output.ToArray();
        }
        private static void SaveBytes(string dir, string name, byte[] bytes, List<string> facts)
        {
            File.WriteAllBytes(Path.Combine(dir, name), bytes);
            using var sha = SHA256.Create();
            facts.Add(name + ": length=" + bytes.Length + "; sha256=" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
        }
        private static string Hash(string path)
        {
            using var sha = SHA256.Create(); using var file = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        private static bool IdentityMatches(List<string> facts, string name)
        {
            try
            {
                var z = typeof(ZLoggerOptions).Assembly;
                var m = typeof(LoggerFactory).Assembly;
                var zh = Hash(z.Location); var mh = Hash(m.Location);
                facts.Add($"{name}: ZLogger={z.FullName}; path={z.Location}; sha256={zh}");
                facts.Add($"{name}: Microsoft.Logging={m.FullName}; path={m.Location}; sha256={mh}");
                return zh == "149f3ae932cbd19a24f0597ce9ff8688e0fbf8070aab2d892c241e92defda5af"
                    && mh == "1a13da2bb23ebdd1af5776b5d49e587d78192b3aa1314e99589fe1cda1b4b16c";
            }
            catch (Exception e) { facts.Add(name + ": identity-error=" + e); return false; }
        }
        private static bool RecordIdentity(List<string> facts)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
                { WorkingDirectory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../..")), UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                if (process == null) throw new InvalidOperationException("git process unavailable");
                var sha = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                facts.Add("checkout-head=" + sha + "; git-exit=" + process.ExitCode);
                facts.Add("unity=" + UnityEngine.Application.unityVersion + "; utc=" + DateTime.UtcNow.ToString("O"));
                return process.ExitCode == 0 && sha.Length == 40 && sha.All(Uri.IsHexDigit);
            }
            catch (Exception e) { facts.Add("checkout-head-error=" + e); return false; }
        }

        private sealed class Observation
        {
            internal bool Completed, ABoth, BLogged, BBoth, BFailed, TimedOut, NoticesRead, IdentityMatches;
            internal int WritesAtB = -1, SpanWritesAtB = -1;
            internal byte[]? Rolling, Realtime;
            internal string[] Notices = Array.Empty<string>();
        }
        private sealed class Probe
        {
            private readonly Gate? _gate;
            private readonly bool _fail;
            internal Probe(Gate? gate, bool fail) { _gate = gate; _fail = fail; }
            public int Value
            {
                get
                {
                    _gate?.Enter();
                    if (_fail)
                    {
                        // Signal during exception unwinding, after the throw has occurred.
                        try { throw new InvalidOperationException("probe failure"); }
                        finally { _gate!.RecordFailure(); }
                    }
                    return 17;
                }
            }
            public override string ToString() => "probe";
        }
        private sealed class Gate
        {
            internal readonly ManualResetEventSlim Both = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim BothFailed = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            private readonly object _lock = new object();
            private readonly HashSet<int> _waiting = new HashSet<int>();
            private readonly List<int> _arrivals = new List<int>();
            private readonly HashSet<int> _failed = new HashSet<int>();
            private int[] _simultaneous = Array.Empty<int>();
            internal bool Simultaneous { get { lock (_lock) return _waiting.Count >= 2; } }
            internal void Enter()
            {
                var id = Thread.CurrentThread.ManagedThreadId;
                lock (_lock)
                {
                    _arrivals.Add(id);
                    _waiting.Add(id);
                    if (_waiting.Count >= 2 && _simultaneous.Length == 0)
                    {
                        _simultaneous = _waiting.ToArray();
                        Both.Set();
                    }
                }
                try { Release.Wait(); }
                finally { lock (_lock) _waiting.Remove(id); }
            }
            internal void RecordFailure()
            {
                lock (_lock)
                {
                    _failed.Add(Thread.CurrentThread.ManagedThreadId);
                    if (_failed.Count >= 2) BothFailed.Set();
                }
            }
            internal string Describe() { lock (_lock) return "arrivals=" + string.Join(",", _arrivals) + "; simultaneous=" + string.Join(",", _simultaneous) + "; failed=" + string.Join(",", _failed) + "; waiting=" + string.Join(",", _waiting); }
        }
        private sealed class ObservedStream : MemoryStream
        {
            private readonly object _sync = new object();
            private int _writes;
            private int _spanWrites;
            internal int ByteArrayWrites => Volatile.Read(ref _writes);
            internal int SpanWrites => Volatile.Read(ref _spanWrites);
            public override void Write(byte[] buffer, int offset, int count)
            { lock (_sync) { Interlocked.Increment(ref _writes); base.Write(buffer, offset, count); } }
            public override void Write(ReadOnlySpan<byte> buffer)
            { lock (_sync) { Interlocked.Increment(ref _spanWrites); base.Write(buffer); } }
            internal byte[] Snapshot() { lock (_sync) return ToArray(); }
        }
    }
}
