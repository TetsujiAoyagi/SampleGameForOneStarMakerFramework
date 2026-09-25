#nullable enable

using System.Runtime.CompilerServices;

// アプリの EditMode テストは SceneTestHelper などの internal ヘルパーを使う。
// ヘルパーをフレームワークの公開 API にせず、SampleGame.Tests だけに見せる。
[assembly: InternalsVisibleTo("SampleGame.Tests")]
