#nullable enable

namespace DebugStudio.App.Tests;

/// <summary>
/// HttpListener / AllocateFreePort を使うテストを直列化する。
/// probe 閉鎖から bind までの TOCTOU でポートが衝突すると、期待フレームが来ず
/// 無制限待ちで testhost が blame-hang されるため、クラス並列を切る。
/// </summary>
[CollectionDefinition("DebugStudioHttpListener", DisableParallelization = true)]
public sealed class DebugStudioHttpListenerCollection
{
}
