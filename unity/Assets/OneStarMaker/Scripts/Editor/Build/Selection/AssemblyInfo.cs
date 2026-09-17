#nullable enable

using System.Runtime.CompilerServices;

// 不正な BuildPlan を使う preflight test のため、internal constructor を既存 test assembly に限定公開する。
// 本番の plan 生成経路や公開 API は増やさない。
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
