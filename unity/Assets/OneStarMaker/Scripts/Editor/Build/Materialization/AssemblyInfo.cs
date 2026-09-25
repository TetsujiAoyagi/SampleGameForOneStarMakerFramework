#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
[assembly: InternalsVisibleTo("SampleGame.DependOnAll.Editor")]
// アプリの build テストが internal な snapshot / AssetDatabase gateway を直接組む。
[assembly: InternalsVisibleTo("SampleGame.Tests.Editor")]
