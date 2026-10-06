#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.DebugCommandOfflineTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run(nameof(Catalog_ExecutesExactName_AndKeepsPayload), Catalog_ExecutesExactName_AndKeepsPayload);
            Run(nameof(Catalog_RejectsEmptyDuplicateAndUnknown), Catalog_RejectsEmptyDuplicateAndUnknown);
            Run(nameof(Catalog_BoundariesPreserveResolutionAndException), Catalog_BoundariesPreserveResolutionAndException);
            Run(nameof(Result_NullAndDefaultHaveDistinctInitialization), Result_NullAndDefaultHaveDistinctInitialization);
            Run(nameof(Catalog_ExecuteDoesNotAllocate), Catalog_ExecuteDoesNotAllocate);
            Run(nameof(Sources_DoNotNameTheScriptMachineOrTheStudio), Sources_DoNotNameTheScriptMachineOrTheStudio);
            GameObjectCommandOffline.Run(Run);
            SetActiveCommandOffline.Run(Run);
            TransformCommandOffline.Run(Run);
            RendererCommandOffline.Run(Run);
            AppOptInCommandOffline.Run(Run);

            Console.WriteLine($"DebugCommand offline: {_passed} passed, {_failed} failed, {_passed + _failed} executed");
            return _failed == 0 && _passed > 0 ? 0 : 1;
        }

        internal static void Run(string name, Action body)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("FAIL " + name);
                Console.WriteLine(ex);
            }
        }

        private static void Catalog_ExecutesExactName_AndKeepsPayload()
        {
            var catalog = new DebugCommandCatalog();
            catalog.Register("sample.echo", payload => DebugCommandResult.Ok("echo", payload));
            DebugCommandResult result;
            True(catalog.TryExecute("sample.echo", "{\"n\":1}", out result), "missing");
            True(result.Success, "success");
            Equal("echo", result.Message, "message");
            Equal("{\"n\":1}", result.PayloadJson, "payload");
            True(!catalog.TryExecute("Sample.echo", "{\"n\":1}", out _), "case folded");
        }

        private static void Catalog_RejectsEmptyDuplicateAndUnknown()
        {
            var catalog = new DebugCommandCatalog();
            Throws<ArgumentException>(() => catalog.Register("", _ => DebugCommandResult.Ok("x", "")));
            catalog.Register("sample.ok", _ => DebugCommandResult.Fail("no"));
            Throws<InvalidOperationException>(() => catalog.Register("sample.ok", _ => DebugCommandResult.Ok("x", "")));
            DebugCommandResult missing;
            True(!catalog.TryExecute("", "{}", out missing), "empty name ran");
            True(!missing.Success, "empty name succeeded");
            Equal("Debug command is not registered.", missing.Message, "unknown message");
        }

        private static void Catalog_BoundariesPreserveResolutionAndException()
        {
            var catalog = new DebugCommandCatalog();
            var calls = 0;
            var callerThread = Environment.CurrentManagedThreadId;
            DebugCommandHandler handler = payload =>
            {
                Equal(callerThread, Environment.CurrentManagedThreadId, "caller thread");
                calls++;
                return DebugCommandResult.Fail(payload);
            };
            Throws<ArgumentException>(() => catalog.Register(null!, handler));
            Throws<ArgumentNullException>(() => catalog.Register("valid", null!));
            catalog.Register(" exact ", handler);
            catalog.Register(" ", handler);
            foreach (var name in new[] { null!, "", "exact", " Exact ", "missing" })
            {
                True(!catalog.TryExecute(name, "opaque", out var missing), "resolved invalid name");
                True(!missing.Success, "missing succeeded");
                Equal("Debug command is not registered.", missing.Message, "missing message");
                Equal("", missing.PayloadJson, "missing payload");
            }
            Equal(0, calls, "missing handler side effects");
            True(catalog.TryExecute(" exact ", "not JSON", out var failed), "registered failure unresolved");
            True(!failed.Success, "business failure succeeded");
            Equal("not JSON", failed.Message, "opaque payload");
            True(catalog.TryExecute(" ", null, out var empty), "whitespace name unresolved");
            Equal("", empty.Message, "null payload");
            Equal(2, calls, "handler calls");
            Equal(2, catalog.Count, "registration count");
            var expected = new InvalidOperationException("handler failed");
            catalog.Register("throw", _ => throw expected);
            try { catalog.TryExecute("throw", "", out _); }
            catch (InvalidOperationException actual)
            {
                True(ReferenceEquals(expected, actual), "exception was replaced");
                return;
            }
            throw new InvalidOperationException("handler exception was swallowed");
        }

        private static void Result_NullAndDefaultHaveDistinctInitialization()
        {
            var constructed = new DebugCommandResult(true, null, null);
            Equal("", constructed.Message, "constructor message");
            Equal("", constructed.PayloadJson, "constructor payload");
            True(default(DebugCommandResult).Message == null, "default message");
            True(default(DebugCommandResult).PayloadJson == null, "default payload");
        }
        private static void Catalog_ExecuteDoesNotAllocate()
        {
            var catalog = new DebugCommandCatalog();
            var ok = DebugCommandResult.Ok("ok", "{}");
            catalog.Register("sample.ok", _ => ok);
            for (var i = 0; i < 16; i++)
            {
                catalog.TryExecute("sample.ok", "{}", out _);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                catalog.TryExecute("sample.ok", "{}", out _);
            }

            Equal(before, GC.GetAllocatedBytesForCurrentThread(), "allocated");
        }

        private static void Sources_DoNotNameTheScriptMachineOrTheStudio()
        {
            var root = FindRepoRoot();
            var directory = Path.Combine(root, "unity", "Assets", "OneStarMaker", "Scripts", "Runtime", "DebugCommands");
            foreach (var file in Directory.GetFiles(directory, "*.cs"))
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);
                True(text.IndexOf("ScriptMachine", StringComparison.Ordinal) < 0, name + " names the script machine");
                True(text.IndexOf("DebugStudio", StringComparison.Ordinal) < 0, name + " names DebugStudio");
                True(text.IndexOf("SampleGame", StringComparison.Ordinal) < 0, name + " names SampleGame");
                True(text.IndexOf("record ", StringComparison.Ordinal) < 0, name + " declares a record");
            }
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "unity", "Assets", "OneStarMaker", "Scripts", "Runtime", "DebugCommands", "DebugCommandCatalog.cs");
                if (File.Exists(candidate))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("リポジトリルートを見つけられません: " + AppContext.BaseDirectory);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " expected " + expected + " actual " + actual);
            }
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Throws<T>(Action body)
            where T : Exception
        {
            try
            {
                body();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("expected " + typeof(T).Name);
        }
    }
}
