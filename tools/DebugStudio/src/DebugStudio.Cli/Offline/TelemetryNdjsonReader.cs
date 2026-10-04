#nullable enable

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugStudio.Export.Models;

namespace DebugStudio.Cli.Offline;

internal sealed record InputLocation(string File, int Line);
internal sealed record LocatedTelemetry(TelemetryExportRecord Record, InputLocation Location);
internal sealed record ComparisonDiagnostic(string Code, string Message, InputLocation? Location = null, InputLocation? RelatedLocation = null);
internal sealed record InputAccounting(long NonblankRows, long BlankRows, long SelectedUniqueRows, long ExactDuplicateRows,
    long SkippedServiceStatusRows, long UnassignedTelemetryRows, long OtherSessionTelemetryRows, long MissingSequenceRows);
internal sealed record TelemetryInput(IReadOnlyList<LocatedTelemetry> SelectedRows, InputAccounting Counts,
    IReadOnlyList<ComparisonDiagnostic> Diagnostics);

internal sealed class ComparisonInputException(int exitCode, string message) : Exception(message)
{
    internal int ExitCode { get; } = exitCode;
}

internal static class TelemetryNdjsonReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions ModelOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions EqualityOptions = new(ModelOptions)
    {
        // Non-metric optional floats can overflow their typed representation; equality still compares typed fields.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    internal static TelemetryInput Read(CompareOptions options)
    {
        var rows = new List<LocatedTelemetry>();
        var diagnostics = new List<ComparisonDiagnostic>();
        var identities = new Dictionary<(string Session, long Sequence), (string Fields, InputLocation Location)>();
        long nonblank = 0, blank = 0, duplicates = 0, service = 0, unassigned = 0, other = 0, missingSequence = 0;

        foreach (var file in options.InputFiles)
        {
            byte[] bytes;
            try
            {
                if (Directory.Exists(file))
                {
                    throw new ComparisonInputException(2, $"Input '{Escape(file)}' is a directory; explicit files are required.");
                }

                bytes = File.ReadAllBytes(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new ComparisonInputException(1, $"Cannot read input '{Escape(file)}': {Escape(ex.Message)}");
            }

            var offset = 0;
            var lineNumber = 0;
            while (offset < bytes.Length)
            {
                lineNumber++;
                var end = Array.IndexOf(bytes, (byte)'\n', offset);
                if (end < 0) end = bytes.Length;
                string line;
                try
                {
                    // Decode one physical line at a time: decoder read-ahead can never misattribute a bad byte.
                    line = StrictUtf8.GetString(bytes, offset, end - offset);
                }
                catch (DecoderFallbackException)
                {
                    throw Invalid(new(file, lineNumber), "Invalid UTF-8 encoding.");
                }

                offset = end + 1;
                if (lineNumber == 1 && line.StartsWith('\uFEFF')) line = line[1..];
                if (line.EndsWith('\r')) line = line[..^1];
                if (string.IsNullOrWhiteSpace(line))
                {
                    blank++;
                    continue;
                }

                nonblank++;
                var location = new InputLocation(file, lineNumber);
                var record = Parse(line, location);
                if (record.Stream == "serviceStatus")
                {
                    service++;
                    continue;
                }

                // Sequence validity belongs to telemetry admission, before any session can be skipped.
                if (record.ProducerSequence is <= 0)
                {
                    throw Invalid(location, "Telemetry producerSequence must be positive when present.");
                }

                if (string.IsNullOrEmpty(record.SessionId))
                {
                    unassigned++;
                    diagnostics.Add(new("unassigned-session", "Telemetry has no sessionId and was skipped.", location));
                    continue;
                }

                if (record.SessionId != options.BaselineSession && record.SessionId != options.CandidateSession)
                {
                    other++;
                    continue;
                }

                if (record.ElapsedMs is double elapsed && (!double.IsFinite(elapsed) || elapsed < 0))
                {
                    throw Invalid(location, "Selected elapsedMs must be finite and nonnegative.");
                }

                if (record.ProducerSequence is long sequence)
                {
                    var identity = (record.SessionId, sequence);
                    var fields = TypedFields(record);
                    if (identities.TryGetValue(identity, out var prior))
                    {
                        if (fields != prior.Fields)
                        {
                            throw Invalid(location, $"Conflicting selected identity (sessionId, producerSequence); first seen at {Format(prior.Location)}.");
                        }

                        duplicates++;
                        diagnostics.Add(new("exact-duplicate", "Exact duplicate selected identity was skipped.", location, prior.Location));
                        continue;
                    }

                    identities.Add(identity, (fields, location));
                }
                else
                {
                    missingSequence++;
                    diagnostics.Add(new("missing-sequence", "Missing producerSequence: this row is retained and deduplication coverage is incomplete.", location));
                }

                rows.Add(new(record, location));
            }
        }

        foreach (var session in new[] { options.BaselineSession, options.CandidateSession })
        {
            if (!rows.Any(row => row.Record.SessionId == session))
            {
                throw new ComparisonInputException(2, $"No valid telemetry rows found for selected session '{Escape(session)}'.");
            }
        }

        return new(rows, new(nonblank, blank, rows.Count, duplicates, service, unassigned, other, missingSequence), diagnostics);
    }

    private static TelemetryExportRecord Parse(string line, InputLocation location)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw Invalid(location, "Expected one normalized JSON object.");
            RejectDuplicateProperties(root, location);
            Require(root, "@timestamp", JsonValueKind.String, location);
            var timestamp = Require(root, "timestampUnixTimeMilliseconds", JsonValueKind.Number, location);
            if (!timestamp.TryGetInt64(out _)) throw Invalid(location, "timestampUnixTimeMilliseconds must be an integer.");
            var stream = Require(root, "stream", JsonValueKind.String, location).GetString();
            if (stream is not ("telemetry" or "serviceStatus")) throw Invalid(location, "Unknown or empty stream.");
            var record = JsonSerializer.Deserialize<TelemetryExportRecord>(root, ModelOptions)!;
            if (stream == "telemetry")
            {
                if (record.SchemaVersion != 3) throw Invalid(location, "Telemetry schemaVersion must be 3.");
                if (string.IsNullOrEmpty(record.Name)) throw Invalid(location, "Telemetry name must be nonempty.");
                if (record.Kind is not ("span" or "sample" or "event")) throw Invalid(location, "Telemetry kind must be span, sample or event.");
            }

            return record;
        }
        catch (JsonException ex)
        {
            // JSON exception messages can contain source fragments; keep the original malformed row private.
            throw Invalid(location, $"Malformed JSON or wrong field type{(ex.Path is null ? "." : $" at {Escape(ex.Path)}.")}");
        }
        catch (InvalidOperationException)
        {
            // JsonDocument defers decoding escaped strings; malformed surrogate escapes can fail on access.
            throw Invalid(location, "Malformed JSON string or unrepresentable typed field.");
        }
    }

    private static JsonElement Require(JsonElement root, string name, JsonValueKind kind, InputLocation location)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != kind)
        {
            throw Invalid(location, $"Required field '{name}' is missing or has the wrong type.");
        }

        return value;
    }

    private static void RejectDuplicateProperties(JsonElement value, InputLocation location)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid(location, $"Duplicate JSON property '{Escape(property.Name)}'.");
                RejectDuplicateProperties(property.Value, location);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item, location);
        }
    }

    private static string TypedFields(TelemetryExportRecord record)
    {
        // Canonicalize typed fields only. Extension JSON is ignored; null and empty tags remain distinct.
        var typed = JsonSerializer.SerializeToElement(record, EqualityOptions);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in typed.EnumerateObject())
            {
                if (property.Name != "tags" || record.Tags is null)
                {
                    writer.WritePropertyName(property.Name);
                    WriteTypedValue(property.Value, writer);
                    continue;
                }

                writer.WriteStartArray("tags");
                foreach (var tag in record.Tags.Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal))
                {
                    writer.WriteStringValue(tag);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        return StrictUtf8.GetString(buffer.ToArray());
    }

    private static void WriteTypedValue(JsonElement value, Utf8JsonWriter writer)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number == 0)
        {
            // Typed floating-point equality equates +0 and -0, including optional payload fields.
            writer.WriteNumberValue(0);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                WriteTypedValue(property.Value, writer);
            }

            writer.WriteEndObject();
        }
        else value.WriteTo(writer);
    }

    internal static string Escape(string text) => string.Concat(text.Select(character => char.IsControl(character)
        ? $"\\u{(int)character:x4}" : character.ToString()));
    internal static string Format(InputLocation location) => $"'{Escape(location.File)}':{location.Line}";
    private static ComparisonInputException Invalid(InputLocation location, string reason) => new(2, $"{Format(location)}: {reason}");
}
