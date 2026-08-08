#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved objects NDJSON 全体の集合。id 引きを提供する。
/// </summary>
public sealed class KibanaSavedObjectBundle
{
    private readonly Dictionary<string, KibanaSavedObject> _byId;

    public KibanaSavedObjectBundle(IReadOnlyList<KibanaSavedObject> objects)
    {
        Objects = objects ?? throw new ArgumentNullException(nameof(objects));
        _byId = new Dictionary<string, KibanaSavedObject>(StringComparer.Ordinal);
        foreach (var obj in objects)
        {
            if (string.IsNullOrEmpty(obj.Id))
            {
                continue;
            }

            // 重複は後勝ち。V2 が重複を指摘する。
            _byId[obj.Id] = obj;
        }
    }

    public IReadOnlyList<KibanaSavedObject> Objects { get; }

    public bool TryGetById(string id, [NotNullWhen(true)] out KibanaSavedObject? savedObject)
    {
        if (string.IsNullOrEmpty(id))
        {
            savedObject = null;
            return false;
        }

        return _byId.TryGetValue(id, out savedObject);
    }
}
