using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneStarMaker.Artifacts.Packaging;

public sealed class PackageFile
{
    public string Path { get; init; } = "";
    public long Bytes { get; init; }
    public string Sha256 { get; init; } = "";
}

public sealed class PackageInfo
{
    public string Path { get; init; } = "";
    public long Bytes { get; init; }
    public string Sha256 { get; init; } = "";
    public string ManifestSha256 { get; init; } = "";
    public PackageFile[] Files { get; init; } = [];
}

public static class PackagePolicy
{
    public const long ArchiveLimit = 256L * 1024 * 1024;
    public const long JsonLimit = 1024 * 1024;
    public const long ExtractLimit = 1024L * 1024 * 1024;
    public const int EntryLimit = 4096;
    public const int RelativePathLimit = 240;
    private static readonly Regex Hex40 = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant);
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex Hex32 = new("^[0-9a-f]{32}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "credentials", "transfers", "credential", "secret", "secrets", "grant", "grants", "token", "tokens",
        "osm.active", "osm.lock", ".aws", ".ssh", ".env", ".git", "id_rsa", "id_ed25519",
        "credentials.json", "service-account.json"
    };
    private static readonly string[] ProtectedExtensions = [".pem", ".p12", ".pfx", ".key", ".kdbx", ".p8"];

    public static void RequireIdentity(string baseRevision, string headRevision, string repositoryId, string runId)
    {
        if (!Hex40.IsMatch(baseRevision) || !Hex40.IsMatch(headRevision) || !Hex64.IsMatch(repositoryId) || !Hex32.IsMatch(runId))
            throw new InvalidDataException("Package identity invalid.");
    }

    public static void RequireEvidenceTask(string? taskId)
    {
        if (taskId is null || !Regex.IsMatch(taskId, "^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Evidence task identity invalid.");
    }
    public static string Relative(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > RelativePathLimit || value.Contains('\\') || value.StartsWith('/') ||
            value.Contains(':') || value.Contains('\0') || value.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe package path.");
        foreach (var segment in value.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Reserved.Contains(segment.Split('.')[0]) ||
                Protected.Contains(segment) || ProtectedExtensions.Any(extension => segment.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unsafe package path.");
        }
        return value;
    }

    public static bool IsProtectedPath(string path) =>
        Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => Protected.Contains(part) || ProtectedExtensions.Any(extension => part.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));

    public static JsonDocument ReadJson(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 1 or > JsonLimit) throw new InvalidDataException("JSON size invalid.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
    }

    public static void Fields(JsonElement element, params string[] required)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("JSON object expected.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON field.");
        if (names.Count != required.Length || required.Any(name => !names.Contains(name)))
            throw new InvalidDataException("JSON fields invalid.");
    }

    public static string String(JsonElement element, string name)
    {
        var item = element.GetProperty(name);
        return item.ValueKind == JsonValueKind.String ? item.GetString()! : throw new InvalidDataException("JSON type invalid.");
    }

    public static long Integer(JsonElement element, string name)
    {
        var item = element.GetProperty(name);
        return item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var value)
            ? value : throw new InvalidDataException("JSON type invalid.");
    }

    public static bool HasReparse(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    public static string Under(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || HasReparse(full))
            throw new InvalidDataException("Path escapes root or follows reparse point.");
        return full;
    }
}
