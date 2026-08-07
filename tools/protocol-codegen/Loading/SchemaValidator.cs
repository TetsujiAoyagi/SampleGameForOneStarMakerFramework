#nullable enable

using System.Text.RegularExpressions;
using ProtocolCodegen.Model;

namespace ProtocolCodegen.Loading;

/// <summary>
/// YAML 読み込み後の不変条件。壊れた契約を黙って Key(0) 化しない。
/// </summary>
public static class SchemaValidator
{
    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.Ordinal)
    {
        "u8", "i32", "i64", "f32", "f64", "bool", "string", "bytes",
    };

    public static void Validate(ProtocolSchema schema)
    {
        if (schema.Meta.Targets.Count == 0)
        {
            throw new InvalidOperationException("meta.yaml emitters.messagepack_csharp.targets is empty.");
        }

        var knownMessages = schema.Messages.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var knownEnums = schema.Enums.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var enumType in schema.Enums)
        {
            if (string.IsNullOrWhiteSpace(enumType.Name))
            {
                throw new InvalidOperationException("Enum is missing name.");
            }

            if (enumType.Surfaces.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Enum '{enumType.Name}' has empty surfaces (excluded everywhere or missing).");
            }

            var seenIds = new HashSet<int>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in enumType.Members)
            {
                if (string.IsNullOrWhiteSpace(member.Name))
                {
                    throw new InvalidOperationException($"Enum '{enumType.Name}' has a member with empty name.");
                }

                if (!seenIds.Add(member.Id))
                {
                    throw new InvalidOperationException($"Enum '{enumType.Name}' has duplicate member id {member.Id}.");
                }

                if (!seenNames.Add(member.Name))
                {
                    throw new InvalidOperationException(
                        $"Enum '{enumType.Name}' has duplicate member name '{member.Name}'.");
                }
            }
        }

        foreach (var message in schema.Messages)
        {
            if (string.IsNullOrWhiteSpace(message.Name))
            {
                throw new InvalidOperationException("Message is missing name.");
            }

            if (message.Surfaces.Count == 0)
            {
                throw new InvalidOperationException($"Message '{message.Name}' has empty surfaces.");
            }

            var seenIds = new HashSet<int>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in message.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    throw new InvalidOperationException($"Message '{message.Name}' has a field with empty name.");
                }

                if (!seenIds.Add(field.Id))
                {
                    throw new InvalidOperationException($"Message '{message.Name}' has duplicate field id {field.Id}.");
                }

                if (!seenNames.Add(field.Name))
                {
                    throw new InvalidOperationException(
                        $"Message '{message.Name}' has duplicate field name '{field.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(field.Type))
                {
                    throw new InvalidOperationException(
                        $"Message '{message.Name}' field '{field.Name}' has empty type.");
                }

                ValidateTypeRef(message.Name, field.Type, knownMessages, knownEnums);
            }
        }
    }

    /// <summary>
    /// Unity surface に CLI control plane が漏れていないことを生成結果で検証する。
    /// </summary>
    public static void ValidateUnitySurfaceIsolation(IReadOnlyDictionary<string, string> generated)
    {
        foreach (var (path, content) in generated)
        {
            var isUnityOutput = path.Contains(
                                    $"{Path.DirectorySeparatorChar}OneStarMaker{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal)
                                || path.Contains("/OneStarMaker/", StringComparison.Ordinal)
                                || path.Contains("\\OneStarMaker\\", StringComparison.Ordinal);
            if (!isUnityOutput)
            {
                continue;
            }

            var fileName = Path.GetFileName(path);
            if (fileName.StartsWith("ControlCommand", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unity surface leak: generated ControlCommand type at {path}");
            }

            if (fileName.Equals("DebugSocketMessageType.cs", StringComparison.Ordinal)
                && (content.Contains("ControlCommandRequest", StringComparison.Ordinal)
                    || content.Contains("ControlCommandResponse", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Unity surface leak: DebugSocketMessageType contains CLI values at {path}");
            }
        }
    }

    private static void ValidateTypeRef(
        string owner,
        string type,
        HashSet<string> knownMessages,
        HashSet<string> knownEnums)
    {
        var arrayMatch = Regex.Match(type, @"^array<(.+)>$");
        if (arrayMatch.Success)
        {
            ValidateTypeRef(owner, arrayMatch.Groups[1].Value, knownMessages, knownEnums);
            return;
        }

        if (PrimitiveTypes.Contains(type) || knownMessages.Contains(type) || knownEnums.Contains(type))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Message '{owner}' references unknown type '{type}'.");
    }
}
