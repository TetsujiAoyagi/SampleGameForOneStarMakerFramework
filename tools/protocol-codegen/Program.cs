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
          --check       generate in memory and fail if committed outputs would change
                        (also fails on orphan auto-generated files under output dirs)
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
if (!repoRoot.EndsWith(Path.DirectorySeparatorChar) && !repoRoot.EndsWith(Path.AltDirectorySeparatorChar))
{
    repoRoot += Path.DirectorySeparatorChar;
}

try
{
    var schema = YamlSchemaLoader.Load(input);
    var emitter = new MessagePackCsharpEmitter(schema, repoRoot);
    var generated = emitter.GenerateAll();
    SchemaValidator.ValidateUnitySurfaceIsolation(generated);

    if (generated.Count == 0)
    {
        Console.Error.WriteLine("No types generated. Check surfaces / YAML input.");
        return 1;
    }

    var dirty = new List<(string Path, string Reason)>();
    foreach (var (path, content) in generated)
    {
        var normalized = content.Replace("\r\n", "\n");
        if (check)
        {
            if (!File.Exists(path))
            {
                dirty.Add((path, "missing"));
                continue;
            }

            var existing = File.ReadAllText(path).Replace("\r\n", "\n");
            if (!string.Equals(existing, normalized, StringComparison.Ordinal))
            {
                dirty.Add((path, "differs"));
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, normalized);
            Console.WriteLine("wrote " + Path.GetRelativePath(repoRoot, path));
        }
    }

    if (check)
    {
        foreach (var orphan in FindOrphanGeneratedFiles(schema, repoRoot, generated.Keys))
        {
            dirty.Add((orphan, "orphan"));
        }

        if (dirty.Count > 0)
        {
            Console.Error.WriteLine("Generated sources are out of date:");
            foreach (var (path, reason) in dirty)
            {
                Console.Error.WriteLine($"  [{reason}] {Path.GetRelativePath(repoRoot, path)}");
            }

            Console.Error.WriteLine("Run tools/protocol-codegen/generate.sh to regenerate.");
            return 1;
        }

        Console.WriteLine($"OK: {generated.Count} generated files match YAML.");
        return 0;
    }

    Console.WriteLine($"Generated {generated.Count} files.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine("protocol-codegen failed: " + ex.Message);
    return 1;
}

static IEnumerable<string> FindOrphanGeneratedFiles(
    ProtocolCodegen.Model.ProtocolSchema schema,
    string repoRoot,
    IEnumerable<string> expectedPaths)
{
    var expected = new HashSet<string>(
        expectedPaths.Select(Path.GetFullPath),
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    var outputDirs = new HashSet<string>(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    foreach (var target in schema.Meta.Targets.Values)
    {
        AddOutputDir(outputDirs, repoRoot, target.OutputDir);
    }

    foreach (var message in schema.Messages)
    {
        foreach (var csharp in message.Csharp.Values)
        {
            if (!string.IsNullOrWhiteSpace(csharp.OutputDir))
            {
                AddOutputDir(outputDirs, repoRoot, csharp.OutputDir!);
            }
        }
    }

    foreach (var dir in outputDirs)
    {
        if (!Directory.Exists(dir))
        {
            continue;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var full = Path.GetFullPath(file);
            if (expected.Contains(full))
            {
                continue;
            }

            // 手書き partial / Protocol helper はヘッダが無い。auto-generated のみ orphan 扱い。
            string head;
            try
            {
                head = File.ReadAllText(full);
            }
            catch
            {
                continue;
            }

            if (head.Contains("// <auto-generated", StringComparison.Ordinal))
            {
                yield return full;
            }
        }
    }
}

static void AddOutputDir(HashSet<string> dirs, string repoRoot, string outputDir)
{
    var dir = outputDir;
    if (!Path.IsPathRooted(dir))
    {
        dir = Path.GetFullPath(Path.Combine(repoRoot, dir));
    }
    else
    {
        dir = Path.GetFullPath(dir);
    }

    dirs.Add(dir);
}
