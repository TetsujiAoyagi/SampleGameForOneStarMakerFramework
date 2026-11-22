#nullable enable

using UnityEngine;

namespace OneStarMaker.Tests.Spikes.PlayerOverlayProbes
{
    // Windows の Player での上書きの登録のスパイク（docs/handoff/PLAYER_OVERLAY_REGISTRATION_SPIKE.md §3）の probe。base の build だけが使う script（SceneKeep・SharedPrefab・v1 の SceneEdit が持つ）。
    // Content Directory の component は script を MonoScript の object で指すので、Player の compile に入る、参照も制約も無い
    // probe だけの asmdef に置く。どの assembly もこの型を参照せず（runner は完全名の文字列と Component だけで扱う）、Player に型が
    // 残るかどうか（P4）を harness が変えないようにする。型を残す仕掛け（[Preserve]・link.xml）を足さない（HANDOFF §4 の禁止）。
    // 値は fixture が SerializedObject で書く。Phase D で削除する。
    public sealed class ProbeBaseOnly : MonoBehaviour
    {
        [SerializeField] private string _tag = "";
        [SerializeField] private int _value;

        public string Tag => _tag;
        public int Value => _value;
    }
}
