#nullable enable

using UnityEngine;

namespace OneStarMaker.Tests.Spikes.OverlayProbes
{
    // 上書きの登録のスパイク（docs/handoff/CONTENT_OVERLAY_REGISTRATION_SPIKE.md §3）の probe。base の build だけが使う script（base の Scene と SharedPrefab が持つ）。
    // Content Directory の component は script を「MonoScript の object」で指すので、player 向けの build に入る assembly に置く
    // （Editor 専用の test assembly は build に入らない）。test 側はこの型を参照せず、完全名と JsonUtility の値で照合する。
    // 値は fixture が SerializedObject で書く。Phase D で削除する。
    public sealed class ProbeBaseOnly : MonoBehaviour
    {
        [SerializeField] private string _tag = "";
        [SerializeField] private int _value;

        public string Tag => _tag;
        public int Value => _value;
    }
}
