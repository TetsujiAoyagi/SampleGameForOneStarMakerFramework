#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SampleGame.Tests")]
// content build の node tag（Role）は Runtime の companion 分類をそのまま使い、規則を複製しない。
[assembly: InternalsVisibleTo("SampleGame.DependOnAll.Editor")]
