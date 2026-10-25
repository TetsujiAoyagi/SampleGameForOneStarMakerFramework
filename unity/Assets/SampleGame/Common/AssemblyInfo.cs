#nullable enable

using System.Runtime.CompilerServices;

// VFX の能力判定の状態への書き込みと、backend への platform の事実の注入を、テストだけから使うため。
[assembly: InternalsVisibleTo("SampleGame.Tests")]
