#nullable enable

using System.Runtime.CompilerServices;

// production API を広げず、pure projection と adapter port を Editor test assembly から検証する。
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
// TargetName など internal な build 契約を、アプリ側へ移した Content Directory 統合テストからも検証する。
[assembly: InternalsVisibleTo("SampleGame.Tests.Editor")]
