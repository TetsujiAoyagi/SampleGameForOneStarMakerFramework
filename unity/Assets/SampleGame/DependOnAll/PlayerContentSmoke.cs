#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;

namespace SampleGame.DependOnAll
{
    internal static class PlayerContentSmoke
    {
        internal enum Stage { Loaded, Instantiated, BehaviorVerified, Destroyed, HandleReleased }

        internal sealed class Result
        {
            internal Result(IReadOnlyList<Stage> completedStages, Exception? failure)
            { CompletedStages = completedStages.ToArray(); Failure = failure; }
            internal IReadOnlyList<Stage> CompletedStages { get; }
            internal Exception? Failure { get; }
        }

        internal static async UniTask<Result> RunAsync(IAssetManagement assets, AppConfig config, CancellationToken ct)
        {
            var representation = config.GetString("content:representation", string.Empty);
            var token = config.GetString("content:probeToken", string.Empty);
            IAssetHandle<GameObject>? handle = null;
            GameObject? instance = null;
            return await RunSequenceAsync(
                async () =>
                {
                    if (representation.Length == 0 || token.Length == 0)
                        throw new InvalidOperationException("BS4 probe configuration is incomplete.");
                    handle = await assets.LoadContentAssetAsync<GameObject>("bs4:fixture:prefab", representation, AssetOwner.Manual, ct);
                    if (handle.Value == null) throw new InvalidOperationException("BS4 probe Prefab load returned null.");
                },
                () =>
                {
                    // Unity Loadable は同じ locator の operation を二度 await できない。Manual handle を
                    // clone の破棄完了まで保持し、一回の native load から代表 Prefab を実体化する。
                    instance = UnityEngine.Object.Instantiate(handle!.Value);
                    return UniTask.CompletedTask;
                },
                () =>
                {
                    Component? probe = null;
                    foreach (var component in instance!.GetComponents<Component>())
                        if (component != null && component.GetType().Name == "Bs4ContentOnlyProbe") { probe = component; break; }
                    if (probe == null) throw new InvalidOperationException("Bs4ContentOnlyProbe was stripped or missing.");
                    var field = probe.GetType().GetField("serializedToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (!string.Equals(field?.GetValue(probe) as string, token, StringComparison.Ordinal)) throw new InvalidOperationException("BS4 probe token mismatch.");
                    instance.SendMessage("VerifyBs4Probe", token, SendMessageOptions.RequireReceiver);
                    var succeeded = probe.GetType().GetField("verificationSucceeded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (succeeded?.GetValue(probe) is not bool value || !value)
                        throw new InvalidOperationException("BS4 probe behavior did not report success.");
                    return UniTask.CompletedTask;
                },
                () => HoldForDeliveryProbeAsync(config, ct),
                async () =>
                {
                    if (instance == null) return;
                    UnityEngine.Object.Destroy(instance);
                    while (instance != null) await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                },
                () =>
                {
                    if (handle != null) assets.Release(handle);
                    return UniTask.CompletedTask;
                });
        }

        private static async UniTask HoldForDeliveryProbeAsync(AppConfig config, CancellationToken ct)
        {
            var root = config.GetString("content:holdSignalRoot", string.Empty);
            if (root.Length == 0) return;

            var full = Path.GetFullPath(root);
            Directory.CreateDirectory(full);
            var ready = Path.Combine(full, "ready.signal");
            var release = Path.Combine(full, "release.signal");
            File.WriteAllText(ready, "ready", new UTF8Encoding(false));
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (!File.Exists(release))
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("Content delivery hold signal timed out.");
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        internal static async UniTask<Result> RunSequenceAsync(
            Func<UniTask> load,
            Func<UniTask> instantiate,
            Func<UniTask> verify,
            Func<UniTask> hold,
            Func<UniTask> destroy,
            Func<UniTask> release)
        {
            Exception? failure = null;
            var completed = new List<Stage>();
            var loaded = false;
            var instantiated = false;
            try
            {
                await load();
                loaded = true;
                completed.Add(Stage.Loaded);
                await instantiate();
                instantiated = true;
                completed.Add(Stage.Instantiated);
                await verify();
                completed.Add(Stage.BehaviorVerified);
                // 診断用 hold は代表 behavior の成立後だけに置く。取消や期限切れでも
                // instantiated は既に確定しているため、finally が clone と handle を必ず回収する。
                await hold();
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (instantiated)
                {
                    try
                    {
                        await destroy();
                        completed.Add(Stage.Destroyed);
                    }
                    catch (Exception cleanupException) { failure = Combine(failure, cleanupException); }
                }
                if (loaded)
                {
                    try { await release(); completed.Add(Stage.HandleReleased); }
                    catch (Exception cleanupException) { failure = Combine(failure, cleanupException); }
                }
            }
            return new Result(completed, failure);
        }

        private static Exception Combine(Exception? primary, Exception cleanup) =>
            primary == null ? cleanup : new AggregateException("Player content verification and cleanup both failed.", primary, cleanup);

        internal static void WriteReceipt(bool succeeded, string stage, IReadOnlyList<string> completedStages, Exception? error)
        {
            var root = Path.GetDirectoryName(Application.dataPath) ?? Application.persistentDataPath;
            var path = Path.Combine(root, "bs4-smoke-receipt.json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(new Receipt { schemaVersion = 2, succeeded = succeeded, stage = stage,
                completedStages = completedStages.ToArray(), error = error?.ToString() ?? "" }, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        [Serializable] private sealed class Receipt { public int schemaVersion; public bool succeeded; public string stage = "";
            public string[] completedStages = Array.Empty<string>(); public string error = ""; }
    }
}
