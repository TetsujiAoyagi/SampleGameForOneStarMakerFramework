#nullable enable

using System.Globalization;
using ProtocolCodegen.Model;
using YamlDotNet.RepresentationModel;

namespace ProtocolCodegen.Loading;

public static class YamlSchemaLoader
{
    public static ProtocolSchema Load(string inputDir)
    {
        var schema = new ProtocolSchema();
        var metaPath = Path.Combine(inputDir, "meta.yaml");
        if (!File.Exists(metaPath))
        {
            throw new FileNotFoundException($"meta.yaml not found: {metaPath}");
        }

        schema.Meta = LoadMeta(metaPath);

        var messagesPath = Path.Combine(inputDir, "messages.yaml");
        if (File.Exists(messagesPath))
        {
            schema.Enums.Add(LoadEnumDocument(messagesPath));
        }

        var enumsPath = Path.Combine(inputDir, "enums.yaml");
        if (File.Exists(enumsPath))
        {
            schema.Enums.AddRange(LoadEnumsDocument(enumsPath));
        }

        var envelopesDir = Path.Combine(inputDir, "envelopes");
        if (Directory.Exists(envelopesDir))
        {
            foreach (var file in Directory.EnumerateFiles(envelopesDir, "*.yaml").OrderBy(x => x, StringComparer.Ordinal))
            {
                schema.Messages.Add(LoadMessageDocument(file));
            }
        }

        return schema;
    }

    private static MetaDocument LoadMeta(string path)
    {
        var root = LoadRoot(path);
        var meta = new MetaDocument
        {
            SchemaVersion = GetInt(root, "schema_version", 1),
            ProtocolName = GetString(root, "protocol_name", "debugsocket"),
        };

        if (root.Children.TryGetValue(new YamlScalarNode("emitters"), out var emittersNode)
            && emittersNode is YamlMappingNode emitters
            && emitters.Children.TryGetValue(new YamlScalarNode("messagepack_csharp"), out var mpNode)
            && mpNode is YamlMappingNode mp
            && mp.Children.TryGetValue(new YamlScalarNode("targets"), out var targetsNode)
            && targetsNode is YamlMappingNode targets)
        {
            foreach (var (keyNode, valueNode) in targets.Children)
            {
                if (valueNode is not YamlMappingNode map)
                {
                    continue;
                }

                var name = ((YamlScalarNode)keyNode).Value ?? "";
                meta.Targets[name] = new EmitterTarget
                {
                    Surface = GetString(map, "surface", name),
                    OutputDir = GetString(map, "output_dir", ""),
                    Namespace = GetString(map, "namespace", ""),
                    NamespaceStyle = GetString(map, "namespace_style", "file_scoped"),
                    Sealed = GetBool(map, "sealed", true),
                    Partial = GetBool(map, "partial", true),
                    NullableEnable = GetBool(map, "nullable_enable", true),
                };
            }
        }

        return meta;
    }

    private static EnumType LoadEnumDocument(string path)
    {
        var root = LoadRoot(path);
        var enumType = new EnumType
        {
            Name = GetString(root, "name", ""),
            Kind = GetString(root, "kind", "enum"),
            Underlying = GetOptionalString(root, "underlying"),
        };
        AddSurfaces(enumType.Surfaces, root, null);

        if (root.Children.TryGetValue(new YamlScalarNode("members"), out var membersNode)
            && membersNode is YamlSequenceNode members)
        {
            foreach (var item in members.Children.OfType<YamlMappingNode>())
            {
                var member = new EnumMember
                {
                    Id = GetInt(item, "id", 0),
                    Name = GetString(item, "name", ""),
                    Value = GetOptionalString(item, "value") ?? GetString(item, "id", "0"),
                };
                AddSurfaces(member.Surfaces, item, enumType.Surfaces);
                enumType.Members.Add(member);
            }
        }

        return enumType;
    }

    private static List<EnumType> LoadEnumsDocument(string path)
    {
        var root = LoadRoot(path);
        var list = new List<EnumType>();
        if (!root.Children.TryGetValue(new YamlScalarNode("enums"), out var enumsNode)
            || enumsNode is not YamlSequenceNode enums)
        {
            return list;
        }

        foreach (var item in enums.Children.OfType<YamlMappingNode>())
        {
            // Reuse by writing temp-like parse: wrap as document shape
            var name = GetString(item, "name", "");
            var enumType = new EnumType
            {
                Name = name,
                Kind = GetString(item, "kind", "enum"),
                Underlying = GetOptionalString(item, "underlying"),
            };
            AddSurfaces(enumType.Surfaces, item, null);
            if (item.Children.TryGetValue(new YamlScalarNode("members"), out var membersNode)
                && membersNode is YamlSequenceNode members)
            {
                foreach (var m in members.Children.OfType<YamlMappingNode>())
                {
                    var member = new EnumMember
                    {
                        Id = GetInt(m, "id", 0),
                        Name = GetString(m, "name", ""),
                        Value = GetOptionalString(m, "value") ?? GetInt(m, "id", 0).ToString(CultureInfo.InvariantCulture),
                    };
                    AddSurfaces(member.Surfaces, m, enumType.Surfaces);
                    enumType.Members.Add(member);
                }
            }

            list.Add(enumType);
        }

        return list;
    }

    private static MessageType LoadMessageDocument(string path)
    {
        var root = LoadRoot(path);
        var message = new MessageType
        {
            Name = GetString(root, "name", ""),
        };
        AddSurfaces(message.Surfaces, root, null);

        if (root.Children.TryGetValue(new YamlScalarNode("csharp"), out var csharpNode)
            && csharpNode is YamlMappingNode csharp)
        {
            foreach (var (keyNode, valueNode) in csharp.Children)
            {
                if (valueNode is not YamlMappingNode map)
                {
                    continue;
                }

                var surface = ((YamlScalarNode)keyNode).Value ?? "";
                message.Csharp[surface] = new CsharpSurfaceOverride
                {
                    Namespace = GetOptionalString(map, "namespace"),
                    OutputDir = GetOptionalString(map, "output_dir"),
                    Partial = map.Children.ContainsKey(new YamlScalarNode("partial"))
                        ? GetBool(map, "partial", true)
                        : null,
                    Sealed = map.Children.ContainsKey(new YamlScalarNode("sealed"))
                        ? GetBool(map, "sealed", true)
                        : null,
                };
            }
        }

        if (root.Children.TryGetValue(new YamlScalarNode("fields"), out var fieldsNode)
            && fieldsNode is YamlSequenceNode fields)
        {
            foreach (var item in fields.Children.OfType<YamlMappingNode>())
            {
                message.Fields.Add(new FieldDef
                {
                    Id = GetInt(item, "id", 0),
                    Name = GetString(item, "name", ""),
                    Type = GetString(item, "type", ""),
                    Optional = GetBool(item, "optional", false),
                    Default = GetDefaultValue(item),
                });
            }
        }

        return message;
    }

    private static YamlMappingNode LoadRoot(string path)
    {
        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidOperationException($"YAML root must be a mapping: {path}");
        }

        return root;
    }

    private static void AddSurfaces(HashSet<string> target, YamlMappingNode node, HashSet<string>? defaults)
    {
        if (node.Children.TryGetValue(new YamlScalarNode("surfaces"), out var surfacesNode))
        {
            if (surfacesNode is YamlSequenceNode seq)
            {
                foreach (var child in seq.Children.OfType<YamlScalarNode>())
                {
                    if (!string.IsNullOrWhiteSpace(child.Value))
                    {
                        target.Add(child.Value);
                    }
                }
            }

            return;
        }

        if (defaults != null)
        {
            foreach (var s in defaults)
            {
                target.Add(s);
            }
        }
    }

    private static string GetString(YamlMappingNode node, string key, string fallback)
    {
        return GetOptionalString(node, key) ?? fallback;
    }

    private static string? GetOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var value) || value is not YamlScalarNode scalar)
        {
            return null;
        }

        return scalar.Value;
    }

    /// <summary>
    /// default: [] は YamlDotNet では SequenceNode になるため、空配列を "[]" に正規化する。
    /// </summary>
    private static string? GetDefaultValue(YamlMappingNode node)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode("default"), out var value))
        {
            return null;
        }

        if (value is YamlSequenceNode seq)
        {
            if (seq.Children.Count == 0)
            {
                return "[]";
            }

            throw new InvalidOperationException("Non-empty sequence defaults are not supported; use a scalar expression.");
        }

        if (value is YamlScalarNode scalar)
        {
            return scalar.Value;
        }

        return null;
    }

    private static int GetInt(YamlMappingNode node, string key, int fallback)
    {
        var s = GetOptionalString(node, key);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static bool GetBool(YamlMappingNode node, string key, bool fallback)
    {
        var s = GetOptionalString(node, key);
        if (s == null)
        {
            return fallback;
        }

        return s.Equals("true", StringComparison.OrdinalIgnoreCase)
               || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || s == "1";
    }
}
