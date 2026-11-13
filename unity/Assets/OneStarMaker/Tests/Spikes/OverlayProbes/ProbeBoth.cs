#nullable enable

using UnityEngine;

namespace OneStarMaker.Tests.Spikes.OverlayProbes
{
    // 上書きの登録のスパイク（docs/handoff/CONTENT_OVERLAY_REGISTRATION_SPIKE.md §3）の probe。両方の build が使う script（fixture の全ての probe の GameObject が持つ）。
    // Content Directory の component は script を「MonoScript の object」で指すので、player 向けの build に入る assembly に置く
    // （test 制約（UNITY_INCLUDE_TESTS）を持つ assembly は、build に define を渡しても player の compile に入らないので、
    // 制約も参照も無い probe 専用の asmdef に置く。HANDOFF §5.2）。test 側はこの型を参照せず、完全名と JsonUtility の値で照合する。
    // 値は fixture が SerializedObject で書く。Phase D で削除する。
    public sealed class ProbeBoth : MonoBehaviour
    {
        [SerializeField] private string _tag = "";
        [SerializeField] private int _value;

        public string Tag => _tag;
        public int Value => _value;
    }
}
