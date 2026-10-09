using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OneStarMaker.Artifacts.Packaging;

public static class PackageIO
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PackageInfo Create(string inputListPath, string operationDirectory,
        string baseRevision, string headRevision, string repositoryId, string runId, int budgetMilliseconds)
        => CreateBounded(inputListPath, operationDirectory, baseRevision, headRevision, repositoryId, runId,
            budgetMilliseconds, PackagePolicy.ArchiveLimit);

    // The caller owns the selection and snapshot. Recheck every byte and the complete file set
    // here so a changed snapshot cannot silently become the archive sent over the network.
    public static PackageInfo CreateVerified(string operationDirectory, string snapshotDirectory,
        string baseRevision, string headRevision, string repositoryId, string runId,
        PackageFile[] expected, int budgetMilliseconds, string? taskId = null)
    {
        PackagePolicy.RequireEvidenceTask(taskId);
        PackagePolicy.RequireIdentity(baseRevision, headRevision, repositoryId, runId);
        if (!Path.IsPathFullyQualified(operationDirectory) ||
            !Path.GetFullPath(snapshotDirectory).Equals(Path.GetFullPath(Path.Combine(operationDirectory, "snapshot")), StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(snapshotDirectory) || PackagePolicy.HasReparse(snapshotDirectory) ||
            expected is null || expected.Length is < 1 or > PackagePolicy.EntryLimit)
            throw new InvalidDataException("Verified snapshot invalid.");
        var timer = Stopwatch.StartNew();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in expected)
        {
            Check(timer, budgetMilliseconds);
            var relative = PackagePolicy.Relative(file.Path);
            if (!names.Add(relative) || file.Bytes is < 0 or > PackagePolicy.ArchiveLimit || !Hex64(file.Sha256))
                throw new InvalidDataException("Expected entry invalid.");
            var path = PackagePolicy.Under(snapshotDirectory, relative);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != file.Bytes || HashFile(path, timer, budgetMilliseconds) != file.Sha256)
                throw new InvalidDataException("Snapshot mismatch.");
            total += file.Bytes;
            if (total > PackagePolicy.ExtractLimit) throw new InvalidDataException("Snapshot too large.");
        }
        var actualNames = Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(snapshotDirectory, path).Replace(Path.DirectorySeparatorChar, '/')).ToArray();
        if (Directory.EnumerateDirectories(snapshotDirectory, "*", SearchOption.AllDirectories).Any(PackagePolicy.HasReparse))
            throw new InvalidDataException("Snapshot reparse point invalid.");
        if (actualNames.Length != names.Count || actualNames.Any(name => !names.Contains(name)))
            throw new InvalidDataException("Snapshot entry set mismatch.");
        var files = expected.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
        var manifest = new {
            schemaVersion = 2, purpose = "evidence", @base = baseRevision, head = headRevision, taskId, artifactId = runId,
            repositoryId, createdAt = DateTimeOffset.UtcNow.ToString("o"),
            files = files.Select(file => new { path = file.Path, bytes = file.Bytes, sha256 = file.Sha256 }).ToArray()
        };
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (manifestBytes.Length > PackagePolicy.JsonLimit) throw new InvalidDataException("Manifest too large.");
        var packagePath = Path.Combine(operationDirectory, "bundle.zip");
        using (var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var bounded = new BoundedWriteStream(output, PackagePolicy.ArchiveLimit))
        using (var zip = new ZipArchive(bounded, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Utf8))
        {
            using (var stream = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open()) stream.Write(manifestBytes);
            foreach (var file in files)
            {
                Check(timer, budgetMilliseconds);
                using var source = new FileStream(PackagePolicy.Under(snapshotDirectory, file.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
                using var destination = zip.CreateEntry("payload/" + file.Path, CompressionLevel.NoCompression).Open();
                if (CopyBounded(source, destination, file.Bytes, timer, budgetMilliseconds) != file.Bytes)
                    throw new InvalidDataException("Snapshot size mismatch.");
            }
        }
        var archive = new FileInfo(packagePath);
        if (archive.Length is < 1 or > PackagePolicy.ArchiveLimit) throw new InvalidDataException("Archive size invalid.");
        return new PackageInfo { Path = packagePath, Bytes = archive.Length,
            Sha256 = HashFile(packagePath, timer, budgetMilliseconds), ManifestSha256 = Hex(SHA256.HashData(manifestBytes)), Files = files };
    }

    // A smaller ceiling is injected by the offline boundary test. Production always uses ArchiveLimit.
    internal static PackageInfo CreateBounded(string inputListPath, string operationDirectory,
        string baseRevision, string headRevision, string repositoryId, string runId, int budgetMilliseconds,
        long archiveLimit)
    {
        if (archiveLimit is < 1 or > PackagePolicy.ArchiveLimit)
            throw new InvalidDataException("Archive limit invalid.");
        PackagePolicy.RequireIdentity(baseRevision, headRevision, repositoryId, runId);
        var timer = Stopwatch.StartNew();
        using var input = PackagePolicy.ReadJson(inputListPath);
        var list = input.RootElement;
        PackagePolicy.Fields(list, "schemaVersion", "purpose", "root", "files");
        if (PackagePolicy.Integer(list, "schemaVersion") != 1 || PackagePolicy.String(list, "purpose") != "synthetic")
            throw new InvalidDataException("Input list invalid.");
        var root = PackagePolicy.String(list, "root");
        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root) ||
            PackagePolicy.HasReparse(root) || PackagePolicy.IsProtectedPath(root))
            throw new InvalidDataException("Source root invalid.");
        var filesJson = list.GetProperty("files");
        if (filesJson.ValueKind != JsonValueKind.Array || filesJson.GetArrayLength() is < 1 or > PackagePolicy.EntryLimit)
            throw new InvalidDataException("Input files invalid.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<PackageFile>();
        var snapshot = Path.Combine(operationDirectory, "snapshot");
        Directory.CreateDirectory(snapshot);
        long total = 0;
        foreach (var item in filesJson.EnumerateArray())
        {
            Check(timer, budgetMilliseconds);
            if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException("Input path type invalid.");
            var relative = PackagePolicy.Relative(item.GetString()!);
            if (relative == "manifest.json" || !names.Add(relative)) throw new InvalidDataException("Duplicate package path.");
            var source = PackagePolicy.Under(root, relative);
            if (!File.Exists(source) || (File.GetAttributes(source) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Source unavailable.");
            var target = PackagePolicy.Under(snapshot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            long count = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using (var reader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var writer = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = reader.Read(buffer)) != 0)
                {
                    Check(timer, budgetMilliseconds);
                    count += read;
                    total += read;
                    if (count > PackagePolicy.ArchiveLimit || total > PackagePolicy.ExtractLimit)
                        throw new InvalidDataException("Package size limit exceeded.");
                    writer.Write(buffer, 0, read);
                    hash.AppendData(buffer, 0, read);
                }
                writer.Flush(true);
            }
            files.Add(new PackageFile { Path = relative, Bytes = count, Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() });
        }
        files.Sort((a, b) => StringComparer.Ordinal.Compare(a.Path, b.Path));
        var manifest = new
        {
            schemaVersion = 1, purpose = "synthetic", @base = baseRevision, head = headRevision,
            repositoryId, runId, createdAt = DateTimeOffset.UtcNow.ToString("o"), retentionSeconds = 86400,
            files = files.Select(file => new { path = file.Path, bytes = file.Bytes, sha256 = file.Sha256 }).ToArray()
        };
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        if (manifestBytes.Length > PackagePolicy.JsonLimit) throw new InvalidDataException("Manifest too large.");
        var packagePath = Path.Combine(operationDirectory, "bundle.zip");
        using (var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var bounded = new BoundedWriteStream(output, archiveLimit))
        using (var zip = new ZipArchive(bounded, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: Utf8))
        {
            using (var stream = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open())
                stream.Write(manifestBytes);
            foreach (var file in files)
            {
                Check(timer, budgetMilliseconds);
                using var source = new FileStream(PackagePolicy.Under(snapshot, file.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
                // The reader enforces a 100:1 ceiling. Store our own entries so highly repetitive
                // synthetic input cannot produce a package that our own fetch would reject.
                using var destination = zip.CreateEntry("payload/" + file.Path, CompressionLevel.NoCompression).Open();
                CopyBounded(source, destination, file.Bytes, timer, budgetMilliseconds);
            }
        }
        var info = new FileInfo(packagePath);
        if (info.Length < 1 || info.Length > archiveLimit) throw new InvalidDataException("Archive size limit exceeded.");
        return new PackageInfo
        {
            Path = packagePath, Bytes = info.Length, Sha256 = HashFile(packagePath, timer, budgetMilliseconds),
            ManifestSha256 = Hex(SHA256.HashData(manifestBytes)), Files = files.ToArray()
        };
    }

    public static PackageInfo Extract(string packagePath, string operationDirectory, string expectedSha256,
        string expectedManifestSha256, string baseRevision, string headRevision, string repositoryId, string runId,
        int budgetMilliseconds, string expectedPurpose = "synthetic", string? taskId = null)
    {
        if (expectedPurpose == "evidence") PackagePolicy.RequireEvidenceTask(taskId);
        PackagePolicy.RequireIdentity(baseRevision, headRevision, repositoryId, runId);
        if (!Hex64(expectedSha256) || !Hex64(expectedManifestSha256)) throw new InvalidDataException("Expected hash invalid.");
        var timer = Stopwatch.StartNew();
        var info = new FileInfo(packagePath);
        if (!info.Exists || info.Length is < 1 or > PackagePolicy.ArchiveLimit)
            throw new InvalidDataException("Archive size invalid.");
        var actual = HashFile(packagePath, timer, budgetMilliseconds);
        if (!string.Equals(actual, expectedSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Archive hash mismatch.");
        using var input = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, false, Utf8);
        if (zip.Entries.Count is < 2 or > PackagePolicy.EntryLimit + 1)
            throw new InvalidDataException("Archive entry count invalid.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ZipArchiveEntry? manifestEntry = null;
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            Check(timer, budgetMilliseconds);
            var name = entry.FullName;
            if (name == "manifest.json") manifestEntry = entry;
            else if (name.StartsWith("payload/", StringComparison.Ordinal)) PackagePolicy.Relative(name[8..]);
            else throw new InvalidDataException("Archive entry invalid.");
            if (!names.Add(name) || name.EndsWith('/') || entry.Length > PackagePolicy.ArchiveLimit ||
                entry.CompressedLength == 0 && entry.Length > 0 ||
                entry.Length > entry.CompressedLength * 100L || IsLink(entry))
                throw new InvalidDataException("Archive header invalid.");
            expanded += entry.Length;
            if (expanded > PackagePolicy.ExtractLimit) throw new InvalidDataException("Expanded archive too large.");
        }
        if (manifestEntry is null || manifestEntry.Length is < 1 or > PackagePolicy.JsonLimit)
            throw new InvalidDataException("Manifest missing.");
        byte[] manifestBytes;
        using (var stream = manifestEntry.Open())
        using (var buffer = new MemoryStream())
        {
            CopyBounded(stream, buffer, PackagePolicy.JsonLimit, timer, budgetMilliseconds);
            manifestBytes = buffer.ToArray();
        }
        if (Hex(SHA256.HashData(manifestBytes)) != expectedManifestSha256)
            throw new InvalidDataException("Manifest hash mismatch.");
        using var doc = JsonDocument.Parse(manifestBytes);
        var manifest = doc.RootElement;
        if (expectedPurpose == "evidence")
            PackagePolicy.Fields(manifest, "schemaVersion", "purpose", "base", "head", "taskId", "artifactId", "repositoryId", "createdAt", "files");
        else PackagePolicy.Fields(manifest, "schemaVersion", "purpose", "base", "head", "repositoryId", "runId", "createdAt", "retentionSeconds", "files");
        var purpose = PackagePolicy.String(manifest, "purpose");
        if (PackagePolicy.Integer(manifest, "schemaVersion") != (expectedPurpose == "evidence" ? 2 : 1) || purpose != expectedPurpose ||
            (taskId is not null && (PackagePolicy.String(manifest, "taskId") != taskId || PackagePolicy.String(manifest, "artifactId") != runId)) ||
            PackagePolicy.String(manifest, "base") != baseRevision || PackagePolicy.String(manifest, "head") != headRevision ||
            PackagePolicy.String(manifest, "repositoryId") != repositoryId ||
            (purpose != "evidence" && (PackagePolicy.String(manifest, "runId") != runId || PackagePolicy.Integer(manifest, "retentionSeconds") != 86400)) ||
            !DateTimeOffset.TryParseExact(PackagePolicy.String(manifest, "createdAt"), "o", null,
                System.Globalization.DateTimeStyles.None, out var created) || created.Offset != TimeSpan.Zero)
            throw new InvalidDataException("Manifest identity invalid.");
        var list = manifest.GetProperty("files");
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() != zip.Entries.Count - 1 ||
            list.GetArrayLength() is < 1 or > PackagePolicy.EntryLimit)
            throw new InvalidDataException("Manifest entries invalid.");
        var files = new List<PackageFile>();
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in list.EnumerateArray())
        {
            PackagePolicy.Fields(item, "path", "bytes", "sha256");
            var relative = PackagePolicy.Relative(PackagePolicy.String(item, "path"));
            var bytes = PackagePolicy.Integer(item, "bytes");
            var hash = PackagePolicy.String(item, "sha256");
            if (!fileNames.Add(relative) || bytes is < 0 or > PackagePolicy.ArchiveLimit || !Hex64(hash) ||
                !names.Contains("payload/" + relative))
                throw new InvalidDataException("Manifest file invalid.");
            var entry = zip.GetEntry("payload/" + relative)!;
            if (entry.Length != bytes) throw new InvalidDataException("Manifest size mismatch.");
            files.Add(new PackageFile { Path = relative, Bytes = bytes, Sha256 = hash });
        }
        var staging = Path.Combine(operationDirectory, "ready.pending");
        Directory.CreateDirectory(staging);
        foreach (var file in files)
        {
            Check(timer, budgetMilliseconds);
            var target = PackagePolicy.Under(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            long copied;
            using (var source = zip.GetEntry("payload/" + file.Path)!.Open())
            using (var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                copied = CopyBounded(source, destination, file.Bytes, timer, budgetMilliseconds);
                destination.Flush(true);
            }
            if (copied != file.Bytes || HashFile(target, timer, budgetMilliseconds) != file.Sha256)
                throw new InvalidDataException("Payload hash mismatch.");
        }
        var ready = Path.Combine(operationDirectory, "ready");
        Directory.Move(staging, ready);
        return new PackageInfo { Path = ready, Bytes = info.Length, Sha256 = actual,
            ManifestSha256 = expectedManifestSha256, Files = files.ToArray() };
    }

    private static long CopyBounded(Stream source, Stream destination, long maximum, Stopwatch timer, int budget)
    {
        var buffer = new byte[64 * 1024];
        long count = 0;
        int read;
        while ((read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, maximum - count + 1))) != 0)
        {
            Check(timer, budget);
            count += read;
            if (count > maximum) throw new InvalidDataException("Stream size limit exceeded.");
            destination.Write(buffer, 0, read);
        }
        return count;
    }

    private sealed class BoundedWriteStream(Stream inner, long maximum) : Stream
    {
        private long written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => written;
        public override long Position { get => written; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximum - written) throw new InvalidDataException("Archive size limit exceeded.");
            inner.Write(buffer);
            written += buffer.Length;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private static string HashFile(string path, Stopwatch timer, int budget)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) != 0) { Check(timer, budget); hash.AppendData(buffer, 0, read); }
        return Hex(hash.GetHashAndReset());
    }

    private static bool IsLink(ZipArchiveEntry entry)
    {
        var unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode != 0 && unixMode != 0x8000;
    }
    private static bool Hex64(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();
    private static void Check(Stopwatch timer, int budget)
    {
        if (budget < 1 || timer.ElapsedMilliseconds >= budget) throw new TimeoutException("Package budget expired.");
    }
}
