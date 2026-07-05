#nullable enable

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Runtime 層で扱う telemetry 用の軽量メモリ snapshot。
    /// 文字列や参照型を持たず、呼び出し元の hot path に余計なアロケーションを入れない。
    /// </summary>
    public readonly struct RuntimeTelemetryMemorySnapshot
    {
        public readonly long ManagedMem;
        public readonly long NativeMem;

        public RuntimeTelemetryMemorySnapshot(long managedMem, long nativeMem)
        {
            ManagedMem = managedMem;
            NativeMem = nativeMem;
        }
    }
}
