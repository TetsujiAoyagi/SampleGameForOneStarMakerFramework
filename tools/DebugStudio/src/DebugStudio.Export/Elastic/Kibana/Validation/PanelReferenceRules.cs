#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DebugStudio.Export.Elastic.Kibana.Validation;

/// <summary>
/// パネルと reference の対応（V3 / V4 / V7）の検算。IO 無しの純関数。
///
/// <para>
/// <see cref="KibanaSavedObjectBundleValidator"/> から切り出した。切り出す前は 399 行あり、
/// §3.3 / A-2 が置いた 380 行の閾値を超えていた（PR #17 フォローアップ Nit #2）。
/// V12 が <see cref="EsqlPanelRules"/> にあるのと同じ分け方。
/// </para>
/// <para>
/// 見るのは 3 つ:
/// <list type="bullet">
/// <item><b>V3</b> — <c>panelsJSON</c> が parse でき、パネルが 1 枚以上あること</item>
/// <item><b>V4</b> — <c>panelRefName</c> と <c>references</c> が 1:1 であること</item>
/// <item><b>V7</b> — どのパネルも<b>中身が解決できる</b>こと（reference か by-value のどちらか）</item>
/// </list>
/// </para>
/// </summary>
public static class PanelReferenceRules
{
    public static void Validate(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        if (bundle is null)
        {
            throw new ArgumentNullException(nameof(bundle));
        }

        ValidateV3V4AndV7(bundle, issues);
    }

    private static void ValidateV3V4AndV7(KibanaSavedObjectBundle bundle, List<KibanaSavedObjectValidationIssue> issues)
    {
        foreach (var obj in bundle.Objects)
        {
            if (!string.Equals(obj.Type, "dashboard", StringComparison.Ordinal))
            {
                continue;
            }

            if (!obj.Attributes.TryGetProperty("panelsJSON", out var panelsJsonProp)
                || panelsJsonProp.ValueKind != JsonValueKind.String)
            {
                issues.Add(CreateIssue("V3", obj, "attributes.panelsJSON が文字列として存在しない。"));
                continue;
            }

            var panelsJsonText = panelsJsonProp.GetString() ?? string.Empty;
            JsonElement panelsArray;
            try
            {
                using var panelsDoc = JsonDocument.Parse(panelsJsonText);
                panelsArray = panelsDoc.RootElement.Clone();
            }
            catch (JsonException)
            {
                issues.Add(CreateIssue("V3", obj, "panelsJSON が JSON として parse できない。"));
                continue;
            }

            if (panelsArray.ValueKind != JsonValueKind.Array)
            {
                issues.Add(CreateIssue("V3", obj, "panelsJSON が JSON 配列ではない。"));
                continue;
            }

            if (panelsArray.GetArrayLength() < 1)
            {
                issues.Add(CreateIssue("V3", obj, "panelsJSON の要素数が 0。パネルが 1 枚以上必要。"));
            }

            var panelRefNames = new HashSet<string>(StringComparer.Ordinal);
            var panelIndex = 0;
            foreach (var panel in panelsArray.EnumerateArray())
            {
                panelIndex++;
                if (!panel.TryGetProperty("panelRefName", out var panelRefNameProp)
                    || panelRefNameProp.ValueKind != JsonValueKind.String
                    || string.IsNullOrEmpty(panelRefNameProp.GetString()))
                {
                    // by-value パネル（ES|QL 等）は panelRefName を持たず、中身が
                    // embeddableConfig.attributes に丸ごと埋まる。中身が解決できるなら V7 は通す。
                    // 「panelRefName が無い」を一律に許すと V7 が塞いだ穴が戻るので、
                    // reference でも by-value でもないパネルだけを赤にする。
                    if (HasByValueAttributes(panel))
                    {
                        continue;
                    }

                    // V7: 存在チェック。V4 は「存在する名前」の 1:1 だけを見る。
                    issues.Add(CreateIssue(
                        "V7",
                        obj,
                        $"panelsJSON[{panelIndex - 1}] に非空の panelRefName も、中身のある "
                        + "embeddableConfig.attributes.state も無い。"));
                    continue;
                }

                panelRefNames.Add(panelRefNameProp.GetString()!);
            }

            var referencePanelNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reference in obj.References)
            {
                var normalized = NormalizePanelReferenceName(reference.Name);
                if (normalized is not null)
                {
                    referencePanelNames.Add(normalized);
                }
            }

            foreach (var name in panelRefNames.Where(n => !referencePanelNames.Contains(n)))
            {
                issues.Add(CreateIssue(
                    "V4",
                    obj,
                    $"panelsJSON の panelRefName '{name}' に対応する references が無い。"));
            }

            foreach (var name in referencePanelNames.Where(n => !panelRefNames.Contains(n)))
            {
                issues.Add(CreateIssue(
                    "V4",
                    obj,
                    $"references の '{name}' が panelsJSON から参照されていない。"));
            }
        }
    }

    /// <summary>
    /// panel の reference 名を <c>panel_*</c> の形に正規化する。panel 参照でなければ null。
    ///
    /// <para>
    /// <b>public なのはテスト（T9）と共有するため。</b> T9 が独自に「<c>:</c> 以降を無条件で剥がす」
    /// 実装を持っていたところ、こちらは <c>panel_</c> で始まる suffix しか受け付けないという
    /// ずれがあり、<c>controlGroup_*</c> のような panel 以外の reference まで T9 側の
    /// 突き合わせ辞書に入っていた。**正規化は 1 箇所に置き、本番とテストで同じものを使う。**
    /// </para>
    ///
    /// <para>
    /// Kibana 8.17 の <c>_export</c> は reference 名に <c>&lt;panelIndex&gt;:</c> の接頭辞を付ける
    /// （<c>p1:panel_p1</c>）。手書き正本は接頭辞無し（<c>panel_p1</c>）。**両方を受け付ける**。
    /// 接頭辞を剥がさないと実 <c>_export</c> が丸ごと V4 で赤になる。
    /// </para>
    /// <para>
    /// <b>緩い実装:</b> 接頭辞が本当にその panel の <c>panelIndex</c> と一致するかまでは見ていない。
    /// V4 の目的は「panelRefName と references の 1:1」であって接頭辞の検算ではない。
    /// </para>
    /// </summary>
    public static string? NormalizePanelReferenceName(string referenceName)
    {
        if (referenceName is null)
        {
            throw new ArgumentNullException(nameof(referenceName));
        }

        if (referenceName.StartsWith("panel_", StringComparison.Ordinal))
        {
            return referenceName;
        }

        var separator = referenceName.LastIndexOf(':');
        if (separator < 0)
        {
            return null;
        }

        var suffix = referenceName[(separator + 1)..];
        return suffix.StartsWith("panel_", StringComparison.Ordinal) ? suffix : null;
    }

    /// <summary>
    /// by-value パネルが「中身を持っている」か。
    ///
    /// <para>
    /// <b><c>attributes</c> が object であるだけでは足りない。</b> <c>attributes: {}</c> は
    /// 中身が空の by-value パネル、つまり V7 が塞ごうとした「参照先が消えたのに気づけない」状態と
    /// 同じものなのに、object であるという理由だけで緑になっていた。
    /// 実体は <c>attributes.state</c>（Lens なら datasource / visualization / query が入る）に
    /// あるので、そこまで非空を要求する。
    /// </para>
    /// <para>
    /// <c>state.query.esql</c> まで要求していないのは、by-value パネルが ES|QL とは限らないため。
    /// ES|QL パネルの中身（<c>FROM</c> と deprecated 語）は V12 が別に見る。
    /// </para>
    /// </summary>
    private static bool HasByValueAttributes(JsonElement panel) =>
        panel.TryGetProperty("embeddableConfig", out var embeddableConfig)
        && embeddableConfig.ValueKind == JsonValueKind.Object
        && embeddableConfig.TryGetProperty("attributes", out var attributes)
        && attributes.ValueKind == JsonValueKind.Object
        && attributes.TryGetProperty("state", out var state)
        && state.ValueKind == JsonValueKind.Object
        && state.EnumerateObject().Any();


    private static KibanaSavedObjectValidationIssue CreateIssue(
        string ruleId,
        KibanaSavedObject obj,
        string detail)
    {
        return new KibanaSavedObjectValidationIssue(
            ruleId,
            obj.LineNumber,
            obj.Id,
            $"行 {obj.LineNumber} (id='{obj.Id}'): {ruleId} — {detail}");
    }
}
