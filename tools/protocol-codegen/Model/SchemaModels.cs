#nullable enable

namespace ProtocolCodegen.Model;

public sealed class ProtocolSchema
{
    public MetaDocument Meta { get; set; } = new();
    public List<EnumType> Enums { get; } = new();
    public List<MessageType> Messages { get; } = new();
}

public sealed class MetaDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ProtocolName { get; set; } = "debugsocket";
    public Dictionary<string, EmitterTarget> Targets { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class EmitterTarget
{
    public string Surface { get; set; } = "";
    public string OutputDir { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string NamespaceStyle { get; set; } = "file_scoped";
    public bool Sealed { get; set; } = true;
    public bool Partial { get; set; } = true;
    public bool NullableEnable { get; set; } = true;
}

public sealed class EnumType
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "enum"; // enum | flags
    public string? Underlying { get; set; }
    public HashSet<string> Surfaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EnumMember> Members { get; } = new();
}

public sealed class EnumMember
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "0";
    public HashSet<string> Surfaces { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MessageType
{
    public string Name { get; set; } = "";
    public HashSet<string> Surfaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<FieldDef> Fields { get; } = new();
    public Dictionary<string, CsharpSurfaceOverride> Csharp { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CsharpSurfaceOverride
{
    public string? Namespace { get; set; }
    public string? OutputDir { get; set; }
    public bool? Partial { get; set; }
    public bool? Sealed { get; set; }
}

public sealed class FieldDef
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Optional { get; set; }
    public string? Default { get; set; }
}
