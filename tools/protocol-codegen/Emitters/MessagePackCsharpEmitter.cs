#nullable enable

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProtocolCodegen.Model;

namespace ProtocolCodegen.Emitters;

public sealed class MessagePackCsharpEmitter
{
    private readonly ProtocolSchema _schema;
    private readonly string _repoRoot;

    public MessagePackCsharpEmitter(ProtocolSchema schema, string repoRoot)
    {
        _schema = schema;
        _repoRoot = repoRoot;
    }

    public IReadOnlyDictionary<string, string> GenerateAll()
    {
        var outputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (targetName, target) in _schema.Meta.Targets)
        {
            foreach (var enumType in _schema.Enums)
            {
                if (!enumType.Surfaces.Contains(target.Surface))
                {
                    continue;
                }

                // surfaces 未指定メンバーは loader が親 surfaces を継承済み。
                // 明示的な surfaces: [] は空集合のまま残り、ここでは除外される。
                var members = enumType.Members
                    .Where(m => m.Surfaces.Contains(target.Surface))
                    .ToList();
                if (members.Count == 0)
                {
                    continue;
                }

                var path = ResolveOutputPath(target, enumType.Name, csharp: null);
                outputs[path] = RenderEnum(enumType, members, target);
            }

            foreach (var message in _schema.Messages)
            {
                if (!message.Surfaces.Contains(target.Surface))
                {
                    continue;
                }

                message.Csharp.TryGetValue(target.Surface, out var csharp);
                var path = ResolveOutputPath(target, message.Name, csharp);
                outputs[path] = RenderMessage(message, target, csharp);
            }
        }

        return outputs;
    }

    private string ResolveOutputPath(EmitterTarget target, string typeName, CsharpSurfaceOverride? csharp)
    {
        var dir = csharp?.OutputDir ?? target.OutputDir;
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.GetFullPath(Path.Combine(_repoRoot, dir));
        }
        else
        {
            dir = Path.GetFullPath(dir);
        }

        var root = Path.GetFullPath(_repoRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar) && !root.EndsWith(Path.AltDirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var fullDir = dir.EndsWith(Path.DirectorySeparatorChar) || dir.EndsWith(Path.AltDirectorySeparatorChar)
            ? dir
            : dir + Path.DirectorySeparatorChar;
        if (!fullDir.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"output_dir escapes repository root: '{dir}' (repoRoot={_repoRoot})");
        }

        // 既存パスと同名で置換し、Unity .meta GUID を維持する。
        return Path.Combine(dir, typeName + ".cs");
    }

    private string RenderEnum(EnumType enumType, List<EnumMember> members, EmitterTarget target)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, target);
        if (enumType.Kind.Equals("flags", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("using System;");
            sb.AppendLine();
        }

        var ns = target.Namespace;
        BeginNamespace(sb, ns, target.NamespaceStyle);

        if (enumType.Kind.Equals("flags", StringComparison.OrdinalIgnoreCase))
        {
            Indent(sb, target, 0);
            sb.AppendLine("[Flags]");
        }

        Indent(sb, target, 0);
        sb.Append("public enum ").Append(enumType.Name);
        if (!string.IsNullOrWhiteSpace(enumType.Underlying))
        {
            sb.Append(" : ").Append(MapPrimitive(enumType.Underlying!));
        }

        sb.AppendLine();
        Indent(sb, target, 0);
        sb.AppendLine("{");
        for (var i = 0; i < members.Count; i++)
        {
            var m = members[i];
            Indent(sb, target, 1);
            sb.Append(m.Name).Append(" = ").Append(m.Value);
            sb.AppendLine(i < members.Count - 1 ? "," : ",");
        }

        Indent(sb, target, 0);
        sb.AppendLine("}");
        EndNamespace(sb, target.NamespaceStyle);
        return NormalizeNewlines(sb.ToString());
    }

    private string RenderMessage(MessageType message, EmitterTarget target, CsharpSurfaceOverride? csharp)
    {
        var sb = new StringBuilder();
        AppendHeader(sb, target);
        sb.AppendLine("using System;");
        sb.AppendLine("using MessagePack;");
        sb.AppendLine();

        var ns = csharp?.Namespace ?? target.Namespace;
        BeginNamespace(sb, ns, target.NamespaceStyle);

        Indent(sb, target, 0);
        sb.AppendLine("[MessagePackObject]");
        Indent(sb, target, 0);
        sb.Append("public ");
        var sealedType = csharp?.Sealed ?? target.Sealed;
        var partial = csharp?.Partial ?? target.Partial;
        if (sealedType)
        {
            sb.Append("sealed ");
        }

        if (partial)
        {
            sb.Append("partial ");
        }

        sb.Append("class ").Append(message.Name).AppendLine();
        Indent(sb, target, 0);
        sb.AppendLine("{");

        foreach (var field in message.Fields.OrderBy(f => f.Id))
        {
            Indent(sb, target, 1);
            sb.Append("[Key(").Append(field.Id.ToString(CultureInfo.InvariantCulture)).AppendLine(")]");
            Indent(sb, target, 1);
            sb.Append("public ").Append(MapFieldType(field)).Append(' ').Append(field.Name)
                .Append(" { get; set; }");
            var defaultExpr = FormatDefault(field);
            if (defaultExpr != null)
            {
                sb.Append(" = ").Append(defaultExpr).Append(';');
            }

            sb.AppendLine();
            sb.AppendLine();
        }

        Indent(sb, target, 0);
        sb.AppendLine("}");
        EndNamespace(sb, target.NamespaceStyle);
        return NormalizeNewlines(sb.ToString());
    }

    private static void AppendHeader(StringBuilder sb, EmitterTarget target)
    {
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// このファイルは protocol/debugsocket YAML から生成されています。");
        sb.AppendLine("// 手編集禁止。契約を変えるときは YAML を直し、tools/protocol-codegen で再生成してください。");
        if (target.NullableEnable)
        {
            sb.AppendLine("#nullable enable");
        }

        sb.AppendLine();
    }

    private static void BeginNamespace(StringBuilder sb, string ns, string style)
    {
        if (style.Equals("braced", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("namespace ").Append(ns).AppendLine();
            sb.AppendLine("{");
        }
        else
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }
    }

    private static void EndNamespace(StringBuilder sb, string style)
    {
        if (style.Equals("braced", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("}");
        }
    }

    private static void Indent(StringBuilder sb, EmitterTarget target, int level)
    {
        var baseIndent = target.NamespaceStyle.Equals("braced", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        sb.Append(new string(' ', (baseIndent + level) * 4));
    }

    private string MapFieldType(FieldDef field)
    {
        var mapped = MapType(field.Type);
        if (!field.Optional || mapped.EndsWith('?'))
        {
            return mapped;
        }

        // optional は値型・参照型とも T? にする（MessagePack nil スロット互換）。
        if (IsValueType(field.Type)
            || IsEnumName(field.Type)
            || IsMessageName(field.Type)
            || mapped is "string" or "byte[]"
            || mapped.EndsWith("[]", StringComparison.Ordinal))
        {
            mapped += "?";
        }

        return mapped;
    }

    private bool IsEnumName(string type) => _schema.Enums.Any(e => e.Name == type);

    private bool IsMessageName(string type) => _schema.Messages.Any(m => m.Name == type);

    private static bool IsValueType(string type)
    {
        return type is "u8" or "i32" or "i64" or "f32" or "f64" or "bool";
    }

    private string MapType(string type)
    {
        var arrayMatch = Regex.Match(type, @"^array<(.+)>$");
        if (arrayMatch.Success)
        {
            return MapType(arrayMatch.Groups[1].Value) + "[]";
        }

        return MapPrimitive(type);
    }

    private static string MapPrimitive(string type) => type switch
    {
        "u8" => "byte",
        "i32" => "int",
        "i64" => "long",
        "f32" => "float",
        "f64" => "double",
        "bool" => "bool",
        "string" => "string",
        "bytes" => "byte[]",
        _ => type,
    };

    private string? FormatDefault(FieldDef field)
    {
        if (field.Default == null)
        {
            return null;
        }

        var d = field.Default;
        if (d is "null" or "~")
        {
            return "null";
        }

        if (d is "[]")
        {
            var elem = MapType(field.Type);
            if (elem.EndsWith("[]", StringComparison.Ordinal))
            {
                var inner = elem[..^2];
                return $"System.Array.Empty<{inner}>()";
            }

            return $"System.Array.Empty<{MapType(StripArray(field.Type))}>()";
        }

        // string 既定値: YAML の "" は YamlDotNet 上で空文字になる。
        if (field.Type == "string" || field.Type == "bytes")
        {
            if (d.Length == 0 || d is "\"\"" or "''" or "string.Empty" or "empty_string")
            {
                return field.Type == "bytes" ? "System.Array.Empty<byte>()" : "string.Empty";
            }

            if ((d.StartsWith('"') && d.EndsWith('"')) || (d.StartsWith('\'') && d.EndsWith('\'')))
            {
                d = d[1..^1];
            }

            return "\"" + d.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        if ((d.StartsWith('"') && d.EndsWith('"')) || (d.StartsWith('\'') && d.EndsWith('\'')))
        {
            var inner = d[1..^1];
            return "\"" + inner.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        if (bool.TryParse(d, out var b))
        {
            return b ? "true" : "false";
        }

        if (Regex.IsMatch(d, @"^-?\d+(\.\d+)?[fFdDmM]?$"))
        {
            if (field.Type == "f32" && !d.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                return d + "f";
            }

            return d;
        }

        // enum / flags のみ修飾する（string 既定値を誤って enum 扱いしない）
        if (IsEnumName(field.Type))
        {
            return QualifyEnumDefault(field.Type, d);
        }

        return d;
    }

    private static string StripArray(string type)
    {
        var m = Regex.Match(type, @"^array<(.+)>$");
        return m.Success ? m.Groups[1].Value : type;
    }

    private string QualifyEnumDefault(string enumTypeName, string expression)
    {
        // Split on | but keep << expressions intact
        var parts = expression.Split('|').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        var qualified = parts.Select(p =>
        {
            if (p.Contains("<<", StringComparison.Ordinal) || Regex.IsMatch(p, @"^\d"))
            {
                return p;
            }

            if (p.Contains('.', StringComparison.Ordinal))
            {
                return p;
            }

            return enumTypeName + "." + p;
        });
        return string.Join(" | ", qualified);
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n");
}
