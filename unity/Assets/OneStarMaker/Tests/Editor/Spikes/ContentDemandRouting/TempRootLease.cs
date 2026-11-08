#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 実験の一時 root の予約。fixture・snapshot の一時 asset と、build した一時 directory を全て
    /// process id を含む 1 つの folder の下に置き、終了時と次回の開始時に回収する（HANDOFF §2 本スライスの制約、N4）。
    /// asset は AssetDatabase が import する必要があるので Assets の下に置く。build した directory は
    /// Unity が import しない `disk~` の下に置く（名前が `~` で終わる folder は AssetDatabase の対象外）。
    /// 予約の lock file は `disk~/lease.json` に process id と process の開始時刻を持ち、
    /// 生きている process の lock は別の予約から消さない（AC10）。死んだ process の lock は回収する（AC9 (b)）。
    /// </summary>
    internal sealed class TempRootLease
    {
        internal const string RootAssetPath = "Assets/OsmSpikeCdr";
        private const string DiskFolderName = "disk~";
        private const string LockFileName = "lease.json";

        private TempRootLease(string leaseId, string token)
        {
            LeaseId = leaseId;
            Token = token;
        }

        internal string LeaseId { get; }
        internal string Token { get; }
        internal string AssetPath => RootAssetPath + "/" + LeaseId;
        internal string DiskPath => Path.Combine(FullPath(AssetPath), DiskFolderName);

        internal static string FullPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        internal static string NewLeaseId()
            => "p" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>予約する。同じ id の lock が生きた process のものなら <see cref="LeaseConflictException"/> を投げ、既存の物に触れない。</summary>
        internal static TempRootLease Acquire(string? leaseId = null)
        {
            var id = leaseId ?? NewLeaseId();
            var lease = new TempRootLease(id, Guid.NewGuid().ToString("N"));
            var lockPath = Path.Combine(lease.DiskPath, LockFileName);
            if (File.Exists(lockPath))
            {
                var existing = ReadLock(lockPath);
                if (existing != null && IsAlive(existing))
                    throw new LeaseConflictException(id, existing.pid);
                // 死んだ process の残骸。先に回収してから予約し直す。
                DeleteLeaseFolder(lease.AssetPath);
            }
            EnsureAssetFolder(RootAssetPath);
            EnsureAssetFolder(lease.AssetPath);
            Directory.CreateDirectory(lease.DiskPath);
            var current = Process.GetCurrentProcess();
            var record = new LockRecord
            {
                leaseId = id, token = lease.Token, pid = current.Id,
                processStartUtcTicks = current.StartTime.ToUniversalTime().Ticks,
            };
            File.WriteAllText(lockPath, JsonUtility.ToJson(record));
            return lease;
        }

        /// <summary>Domain Reload の後に、自分が予約した lease へ戻る。token が違えば別の予約なので戻らない。</summary>
        internal static TempRootLease? Attach(string leaseId, string token)
        {
            var lease = new TempRootLease(leaseId, token);
            var record = ReadLock(Path.Combine(lease.DiskPath, LockFileName));
            return record != null && record.token == token ? lease : null;
        }

        internal string EnsureAssetChild(string name)
        {
            var path = AssetPath + "/" + name;
            EnsureAssetFolder(path);
            return path;
        }

        internal string EnsureDiskChild(string name)
        {
            var path = Path.Combine(DiskPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        internal bool Release()
        {
            DeleteLeaseFolder(AssetPath);
            DeleteRootIfEmpty();
            return !Directory.Exists(FullPath(AssetPath));
        }

        /// <summary>
        /// 死んだ process の lease folder を回収する。生きた process（自分を含む）の lease は残す。
        /// lock の無い folder は名前の process id で判定する（folder 作成と lock 書き込みの間の crash）。
        /// </summary>
        internal static List<string> ReclaimStale()
        {
            var reclaimed = new List<string>();
            var root = FullPath(RootAssetPath);
            if (!Directory.Exists(root)) return reclaimed;
            foreach (var directory in Directory.GetDirectories(root))
            {
                var name = Path.GetFileName(directory);
                var record = ReadLock(Path.Combine(directory, DiskFolderName, LockFileName));
                var alive = record != null ? IsAlive(record) : IsPidAliveFromName(name);
                if (alive) continue;
                DeleteLeaseFolder(RootAssetPath + "/" + name);
                reclaimed.Add(name);
            }
            DeleteRootIfEmpty();
            return reclaimed;
        }

        /// <summary>AC9 (b) の残骸の模擬に使う、存在しない process id。</summary>
        internal static int FindDeadPid()
        {
            for (var pid = int.MaxValue - 3; pid > int.MaxValue - 4000; pid -= 4)
            {
                if (!IsPidAlive(pid, null)) return pid;
            }
            throw new InvalidOperationException("No unused process id was found for the stale lease fixture.");
        }

        /// <summary>死んだ process の lock を持つ偽の lease folder を作る（AC9 (b)）。lease folder の asset path を返す。</summary>
        internal static string SeedStaleLease(int deadPid)
        {
            var id = "p" + deadPid.ToString(CultureInfo.InvariantCulture) + "-deadbeef";
            var path = RootAssetPath + "/" + id;
            EnsureAssetFolder(RootAssetPath);
            EnsureAssetFolder(path);
            var disk = Path.Combine(FullPath(path), DiskFolderName);
            Directory.CreateDirectory(disk);
            var record = new LockRecord { leaseId = id, token = "stale", pid = deadPid, processStartUtcTicks = 0 };
            File.WriteAllText(Path.Combine(disk, LockFileName), JsonUtility.ToJson(record));
            return path;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)!.Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        private static void DeleteLeaseFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            // `disk~` は AssetDatabase の対象外なので、DeleteAsset の後に残っていれば file system から消す。
            var full = FullPath(assetPath);
            if (Directory.Exists(full)) Directory.Delete(full, true);
            if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
        }

        private static void DeleteRootIfEmpty()
        {
            var root = FullPath(RootAssetPath);
            if (!Directory.Exists(root) || Directory.GetFileSystemEntries(root).Length != 0) return;
            AssetDatabase.DeleteAsset(RootAssetPath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (File.Exists(root + ".meta")) File.Delete(root + ".meta");
        }

        private static LockRecord? ReadLock(string path)
        {
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<LockRecord>(File.ReadAllText(path)); }
            catch (ArgumentException) { return null; }
        }

        private static bool IsAlive(LockRecord record) => IsPidAlive(record.pid, record.processStartUtcTicks);

        private static bool IsPidAliveFromName(string name)
        {
            var dash = name.IndexOf('-');
            if (!name.StartsWith("p", StringComparison.Ordinal) || dash < 2) return false;
            return int.TryParse(name.Substring(1, dash - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                && IsPidAlive(pid, null);
        }

        // process id は再利用されるので、lock に書いた開始時刻と照合する。開始時刻が分からない時は id だけで判定する。
        private static bool IsPidAlive(int pid, long? startUtcTicks)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited) return false;
                if (startUtcTicks == null || startUtcTicks == 0) return true;
                var delta = Math.Abs(process.StartTime.ToUniversalTime().Ticks - startUtcTicks.Value);
                return delta < TimeSpan.TicksPerSecond;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return true; }
        }

        [Serializable]
        private sealed class LockRecord
        {
            public string leaseId = "";
            public string token = "";
            public int pid;
            public long processStartUtcTicks;
        }
    }

    internal sealed class LeaseConflictException : Exception
    {
        internal LeaseConflictException(string leaseId, int ownerPid)
            : base($"Temp root '{leaseId}' is reserved by live process {ownerPid}.")
        {
            LeaseId = leaseId;
            OwnerPid = ownerPid;
        }

        internal string LeaseId { get; }
        internal int OwnerPid { get; }
    }
}
