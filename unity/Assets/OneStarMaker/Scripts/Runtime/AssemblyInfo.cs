#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OneStarMaker.Tests")]
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
[assembly: InternalsVisibleTo("OneStarMaker.Editor")]
// 移したアプリの EditMode は SceneBase の寿命フックと AssetManagement のテスト用コンストラクタを使う。
// これらを本番 API にせず、移先の SampleGame.Tests だけに見せる。
[assembly: InternalsVisibleTo("SampleGame.Tests")]
// Content Directory の統合テストはアプリの bootstrap 合成と一緒に SampleGame.Tests.Editor へ置いた。
// RegisterVerified などの internal を本番 API にせず、そのテストアセンブリだけに見せる。
[assembly: InternalsVisibleTo("SampleGame.Tests.Editor")]
