#nullable enable

using ProtocolCodegen.Emitters;
using ProtocolCodegen.Loading;

static int Usage()
{
    Console.Error.WriteLine("""
        protocol-codegen — DebugSocket YAML → MessagePack C# emitter

        Usage:
          protocol-codegen --input <dir> [--repo-root <dir>] [--check]
          protocol-codegen --input <dir> --emit messagepack-csharp [--check]

        Options:
          --input       protocol YAML directory (e.g. protocol/debugsocket)
          --repo-root   repository root for resolving relative output_dir (default: cwd)
          --check       generate to memory and fail if committed outputs would change
          --emit        emitter name (only messagepack-csharp is supported)
        """);
    return 2;
}

var input = "";
var repoRoot = Directory.GetCurrentDirectory();
var check = false;
var emit = "messagepack-csharp";

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input" when i + 1 < args.Length:
            input = args[++i];
            break;
        case "--repo-root" when i + 1 < args.Length:
            repoRoot = args[++i];
            break;
        case "--emit" when i + 1 < args.Length:
            emit = args[++i];
            break;
        case "--check":
            check = true;
            break;
        case "--help" or "-h":
            return Usage();
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return Usage();
    }
}

if (string.IsNullOrWhiteSpace(input))
{
    return Usage();
}

if (!emit.Equals("messagepack-csharp", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Unsupported emitter: {emit}");
    return 2;
}

input = Path.GetFullPath(input);
repoRoot = Path.GetFullPath(repoRoot);

var schema = YamlSchemaLoader.Load(input);
var emitter = new MessagePackCsharpEmitter(schema, repoRoot);
var generated = emitter.GenerateAll();

if (generated.Count == 0)
{
    Console.Error.WriteLine("No types generated. Check surfaces / YAML input.");
    return 1;
}

var dirty = new List<string>();
foreach (var (path, content) in generated)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var normalized = content.Replace("\r\n", "\n");
    if (check)
    {
        if (!File.Exists(path))
        {
            dirty.Add(path + " (missing)");
            continue;
        }

        var existing = File.ReadAllText(path).Replace("\r\n", "\n");
        if (!string.Equals(existing, normalized, StringComparison.Ordinal))
        {
            dirty.Add(path);
        }
    }
    else
    {
        File.WriteAllText(path, normalized);
        Console.WriteLine("wrote " + Path.GetRelativePath(repoRoot, path));
    }
}

if (check)
{
    if (dirty.Count > 0)
    {
        Console.Error.WriteLine("Generated sources are out of date:");
        foreach (var d in dirty)
        {
            Console.Error.WriteLine("  " + Path.GetRelativePath(repoRoot, d));
        }

        Console.Error.WriteLine("Run tools/protocol-codegen/generate.sh to regenerate.");
        return 1;
    }

    Console.WriteLine($"OK: {generated.Count} generated files match YAML.");
    return 0;
}

Console.WriteLine($"Generated {generated.Count} files.");
return 0;
