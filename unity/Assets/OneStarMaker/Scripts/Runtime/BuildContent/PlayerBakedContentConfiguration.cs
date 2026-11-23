#nullable enable
using System;
using OneStarMaker.Foundation.Config;
namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>
    /// Player の 1 回の起動の種類。build の時に baked config の protected key <c>content:playerKind</c> で決まる。
    /// <c>Smoke</c> は動作確認の Player（初回 Scene の後に fixture を検証し、unload・close して receipt を書いて終わる）、
    /// <c>Playable</c> は初回 Scene を load したまま起動を終え、利用者の操作を受け付け続ける Player。
    /// 「Kind」は Player の起動の種類を指し、directory の登録の寿命（ContentDirectorySession）や Scene の InGameSession とは別の語である。
    /// </summary>
    internal enum PlayerContentKind { Smoke, Playable }

    internal sealed class PlayerBakedContentConfiguration
    {
        internal const string PlayerKindKey = "content:playerKind";

        internal PlayerBakedContentConfiguration(AppConfig config)
        { SchemaVersion=config.GetInt("content:schemaVersion",0);RuntimeMode=config.GetString("content:runtimeMode","");BuildIdentity=config.GetString("content:buildIdentity","");ContentSet=config.GetString("content:contentSet","");Target=config.GetString("content:target","");Representation=config.GetString("content:representation","");FirstScene=config.GetString("content:firstScene","");RelativeDirectory=config.GetString("content:relativeDirectory","");ProbeToken=config.GetString("content:probeToken","");
          PlayerKindRaw=config.GetString(PlayerKindKey,"");PlayerKind=ParsePlayerKind(PlayerKindRaw,BuildIdentity,Target); }
        internal int SchemaVersion{get;}internal string RuntimeMode{get;}internal string BuildIdentity{get;}internal string ContentSet{get;}internal string Target{get;}internal string Representation{get;}internal string FirstScene{get;}internal string RelativeDirectory{get;}internal string ProbeToken{get;}

        /// <summary>baked の生の値。protected の比較はこの値と重ねた後の値を ordinal に比べる（読んだ enum と比べると大小違いの override が通りうる）。</summary>
        internal string PlayerKindRaw{get;}
        internal PlayerContentKind PlayerKind{get;}

        /// <summary>
        /// key は必須で、無い・空・<c>smoke</c>/<c>playable</c> 以外（大小違いを含む）は fail-closed にする。
        /// 既定を置かない理由: baked は coordinator が同じ build の exe と対で書き、build identity は build ごとに新しいので、
        /// この key を知る exe が key の無い baked を読む正当な場面が無い。
        /// </summary>
        internal static PlayerContentKind ParsePlayerKind(string raw, string? buildIdentity = null, string? target = null) => raw switch
        {
            "smoke" => PlayerContentKind.Smoke,
            "playable" => PlayerContentKind.Playable,
            _ => throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidConfiguration,
                $"Baked Player kind '{raw}' is missing or invalid.", buildIdentity, target),
        };

        /// <summary>
        /// 失敗の通知が読む種類の latch。baked の検証より前に、生の値だけで決める（検証が投げても latch は残す）。
        /// ordinal に <c>playable</c> の時だけ Playable で、file が読めない・key の欠け・不正な値はどれも Smoke
        /// （その Player は動作確認の失敗の receipt を書く）。
        /// </summary>
        internal static PlayerContentKind LatchPlayerKind(string? raw) =>
            string.Equals(raw, "playable", StringComparison.Ordinal) ? PlayerContentKind.Playable : PlayerContentKind.Smoke;
    }
}
