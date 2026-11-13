#nullable enable

using System.Runtime.CompilerServices;

// 不正な BuildPlan を使う preflight test のため、internal constructor を既存 test assembly に限定公開する。
// 本番の plan を作り直す道は public な BuildPlan.WithSelectedSubset（selected の部分集合だけ）に限り、
// 任意の plan を作れる internal constructor を本番の assembly へは開かない。
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
[assembly: InternalsVisibleTo("SampleGame.DependOnAll.Editor")]
// アプリの build テストが internal な BuildPlan コンストラクタで不正計画を作る。
[assembly: InternalsVisibleTo("SampleGame.Tests.Editor")]
