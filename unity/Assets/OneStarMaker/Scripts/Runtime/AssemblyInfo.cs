#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OneStarMaker.Tests")]
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
[assembly: InternalsVisibleTo("OneStarMaker.Editor")]
// Content Directory の統合テストはアプリの bootstrap 合成と一緒に SampleGame.Tests.Editor へ置いた。
// RegisterVerified などの internal を本番 API にせず、そのテストアセンブリだけに見せる。
[assembly: InternalsVisibleTo("SampleGame.Tests.Editor")]
