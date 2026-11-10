#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Build.Selection;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 宣言から Editor Play の config（process 環境変数）を作る変換。Play As・検証 Play・From published directory が共有する。
    /// <para>
    /// 環境変数の prefix と、表現の主値 <c>Full</c> を Variant の空へ写す規則は SampleGame の決まりで、Framework は持たない
    /// （Framework の ResolveSceneVariant は <c>assets:sceneVariant</c> を読むだけ）。空への写像は Play As の
    /// <c>assets:sceneVariant</c> だけに使い、directory の <c>content:representation</c> は主値の文字列（<c>Full</c> を含む）のまま置く。
    /// directory の起動は representation の空を拒否するので、ここを共有の写像にしない。
    /// </para>
    /// </summary>
    internal static class PurposeLaunchSettings
    {
        // AppInitializer.GetEnvironmentVariablePrefix と同じ値。Runtime の protected member なので Editor からは引けない。
        internal const string EnvironmentPrefix = "SAMPLEGAME_";
        internal const string RuntimeModeKey = "content:runtimeMode";
        internal const string SceneVariantKey = "assets:sceneVariant";
        internal const string FirstSceneKey = "content:firstScene";
        internal const string AppConfigPath = "Assets/SampleGame/Config/app-config.json";

        /// <summary>config key（<c>a:b</c>）を、EnvironmentVariableConfigProvider が同じ key へ読み戻す環境変数名にする。</summary>
        internal static string EnvironmentName(string configKey)
        {
            if (string.IsNullOrWhiteSpace(configKey)) throw new ArgumentException("Config key is required.", nameof(configKey));
            return EnvironmentPrefix + configKey.Replace(":", "__").ToUpperInvariant();
        }

        /// <summary>表現の主値を Play As の <c>assets:sceneVariant</c> へ写す。Full は空（Variant 無し）。</summary>
        internal static string SceneVariantOf(string primary) => primary switch
        {
            "Full" => string.Empty,
            "Whitebox" => "Whitebox",
            _ => throw new ArgumentException("Unsupported representation for Play As: " + primary, nameof(primary)),
        };

        /// <summary>
        /// Play As の config。<see cref="LaunchSettings.Placed"/> を環境に置き、<see cref="LaunchSettings.Controlled"/> の全 key を
        /// 外の値・コマンドラインとの競合の検査に掛ける。主値 Full の <c>assets:sceneVariant</c> は空で、空文字の環境変数は
        /// key の削除と同じになるので置かない。その実効値は下位の層（app-config）の値になるため
        /// <see cref="LaunchSettings.RequiresEmptyLowerLayer"/> に挙げ、呼び手が下位の層の空を確かめる。
        /// </summary>
        internal static LaunchSettings PlayAs(BuildPurposeDeclaration declaration)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            var variant = SceneVariantOf(declaration.Representation.Primary);
            var placed = new List<KeyValuePair<string, string>> { new(RuntimeModeKey, "addressables") };
            var emptyLower = new List<string>();
            if (variant.Length == 0) emptyLower.Add(SceneVariantKey);
            else placed.Add(new KeyValuePair<string, string>(SceneVariantKey, variant));
            placed.AddRange(declaration.RuntimeDefaults);
            var controlled = placed.Select(x => x.Key).Concat(emptyLower).ToArray();
            return new LaunchSettings(placed, controlled, emptyLower);
        }

        /// <summary>
        /// directory の検証 Play の環境変数の集合。content の 5 key は Delivery 窓と同じ値（<see cref="ContentDeliveryPlayBridge.ContentValues"/>）、
        /// そのうえに manifest の宣言の runtime 既定値と、App が宣言に決めた初回 Scene を置く。
        /// 検証 Play と From published directory はどちらもここを通るので、初回 Scene は引数で受けたものだけが入る。
        /// </summary>
        internal static IReadOnlyList<KeyValuePair<string, string>> DirectoryPlay(
            string revisionRoot, string manifestSha256, string identity, string representation,
            IEnumerable<KeyValuePair<string, string>> runtimeDefaults, string firstScene)
        {
            if (string.IsNullOrWhiteSpace(firstScene)) throw new ArgumentException("First scene is required.", nameof(firstScene));
            var values = new List<KeyValuePair<string, string>>(
                ContentDeliveryPlayBridge.ContentValues(revisionRoot, manifestSha256, identity, representation));
            values.Add(new KeyValuePair<string, string>(EnvironmentName(FirstSceneKey), firstScene));
            foreach (var pair in runtimeDefaults)
                values.Add(new KeyValuePair<string, string>(EnvironmentName(pair.Key), pair.Value));
            return values;
        }

        /// <summary><see cref="DirectoryPlay"/> が置く config key。値は install の後まで決まらないが、競合の検査は install の前に行う。</summary>
        internal static IReadOnlyList<string> DirectoryPlayConfigKeys(IEnumerable<KeyValuePair<string, string>> runtimeDefaults) =>
            ContentDeliveryPlayBridge.ContentConfigKeys.Append(FirstSceneKey).Concat(runtimeDefaults.Select(x => x.Key)).ToArray();

        /// <summary>
        /// 混成の経路の環境変数。検証 Play の値から初回 Scene を除き（開いている Scene の複製から始まる）、snapshot を宣言した印を足す。
        /// 環境変数は空文字を置けないので、初回 Scene の実効値を空にするには下位の層（app-config）に値が無いことを入口が別に確かめる。
        /// 印の key は <c>content:</c> の名前空間ではなく、Player の protected config の照合に入らない。
        /// </summary>
        internal static IReadOnlyList<KeyValuePair<string, string>> SnapshotPlay(IEnumerable<KeyValuePair<string, string>> directoryPlay)
        {
            var firstScene = EnvironmentName(FirstSceneKey);
            var expected = EnvironmentName(SnapshotExpectedKey);
            var values = directoryPlay
                .Where(x => !string.Equals(x.Key, firstScene, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(x.Key, expected, StringComparison.OrdinalIgnoreCase))
                .ToList();
            values.Add(new KeyValuePair<string, string>(expected, "true"));
            return values;
        }

        /// <summary>snapshot を宣言した印の config key（Framework の Play snapshot の session が持つ値）。</summary>
        internal const string SnapshotExpectedKey = OneStarMaker.Editor.PlaySnapshot.PlaySnapshotSession.ExpectedConfigKey;

        /// <summary>混成の経路が実効値を保証する config key（競合の検査に掛ける）。</summary>
        internal static IReadOnlyList<string> SnapshotPlayConfigKeys(IEnumerable<KeyValuePair<string, string>> runtimeDefaults) =>
            DirectoryPlayConfigKeys(runtimeDefaults).Append(SnapshotExpectedKey).ToArray();

        internal sealed class LaunchSettings
        {
            internal LaunchSettings(IReadOnlyList<KeyValuePair<string, string>> placed, IReadOnlyList<string> controlled,
                IReadOnlyList<string> requiresEmptyLowerLayer)
            { Placed = placed; Controlled = controlled; RequiresEmptyLowerLayer = requiresEmptyLowerLayer; }

            /// <summary>環境に置く config key=value。</summary>
            internal IReadOnlyList<KeyValuePair<string, string>> Placed { get; }
            /// <summary>この経路が実効値を保証する config key（置かない key を含む）。</summary>
            internal IReadOnlyList<string> Controlled { get; }
            /// <summary>置かずに、下位の層（app-config）が空であることで実効値を空にする config key。</summary>
            internal IReadOnlyList<string> RequiresEmptyLowerLayer { get; }

            internal IReadOnlyList<KeyValuePair<string, string>> PlacedEnvironment =>
                Placed.Select(x => new KeyValuePair<string, string>(EnvironmentName(x.Key), x.Value)).ToArray();
        }
    }
}
