#nullable enable

using System.Runtime.CompilerServices;

// production API を広げず、pure projection と adapter port を Editor test assembly から検証する。
[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
