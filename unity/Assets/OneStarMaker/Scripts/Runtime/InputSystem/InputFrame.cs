#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 公開スナップショットの状態。デバイス I/O と R3 は持たない。
    /// プロファイル ID、有効マップ、SceneState、バッファは同じ公開結果を決めるので同じ寿命に置く。
    /// 値の配信は Observable にしない。毎フレームの OnNext は割り当てになり、ここが読むのはレベルだからである。
    /// </summary>
    internal sealed class InputFrame
    {
        private readonly InputActionSlot[] _slots;
        private readonly InputActionValue[] _sampled;
        private readonly InputActionValue[] _published;
        private readonly Dictionary<InputActionNameKey, int> _lookup;
        private InputControlMap _activeMap;
        private InputProfileId _profile;
        private SceneState _interactionState;

        public InputFrame(InputActionSlot[] slots)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            _slots = new InputActionSlot[slots.Length];
            _lookup = new Dictionary<InputActionNameKey, int>(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (string.IsNullOrEmpty(slot.Name))
                {
                    throw new ArgumentException($"スロット {i} のアクション名が空です。", nameof(slots));
                }

                if (slot.Index != i)
                {
                    throw new ArgumentException(
                        $"スロット {i} の Index が {slot.Index} です。束ね順と一致させます。",
                        nameof(slots));
                }

                if (slot.Map != InputControlMap.Player && slot.Map != InputControlMap.UI)
                {
                    throw new ArgumentException($"スロット {i} のマップが未定義です。", nameof(slots));
                }

                var key = new InputActionNameKey(slot.Name, slot.Map);
                if (!_lookup.TryAdd(key, i))
                {
                    throw new ArgumentException(
                        $"アクション '{slot.Name}' が {slot.Map} に重複しています。",
                        nameof(slots));
                }

                _slots[i] = slot;
            }

            _sampled = new InputActionValue[_slots.Length];
            _published = new InputActionValue[_slots.Length];
            InputPublication.WriteNeutral(_slots, _sampled);
            InputPublication.WriteNeutral(_slots, _published);
            _activeMap = InputControlMap.Player;
            _profile = InputProfileId.Default;
            _interactionState = SceneState.None;
        }

        public int ActionCount => _slots.Length;

        public InputControlMap ActiveMap => _activeMap;

        public InputProfileId ActiveProfile => _profile;

        public SceneState InteractionState => _interactionState;

        public ReadOnlySpan<InputActionValue> Published => _published;

        public bool TryGetActionIndex(string? actionName, InputControlMap map, out int index)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                index = -1;
                return false;
            }

            return _lookup.TryGetValue(new InputActionNameKey(actionName, map), out index);
        }

        public bool TryRead(int index, out InputActionValue value)
        {
            if ((uint)index >= (uint)_published.Length)
            {
                value = default;
                return false;
            }

            value = _published[index];
            return true;
        }

        public void SetInteractionState(SceneState state)
        {
            _interactionState = state;
        }

        /// <summary>
        /// 既定以外は拒否し、現在の ID を変えない。バインドの差し替えはしない。
        /// </summary>
        public bool TrySelectProfile(InputProfileId requested)
        {
            if (!requested.Equals(InputProfileId.Default))
            {
                return false;
            }

            _profile = InputProfileId.Default;
            return true;
        }

        public bool TrySetMap(InputControlMap map, out bool changed)
        {
            if (map != InputControlMap.Player && map != InputControlMap.UI)
            {
                changed = false;
                return false;
            }

            changed = map != _activeMap;
            _activeMap = map;
            return true;
        }

        /// <summary>
        /// Stable のときだけリーダーを呼ぶ。それ以外は公開バッファを零で書き直す。
        /// リーダーを呼ばないのは、公開契約がデバイスサンプルを必要とせず、
        /// 非 Stable 中に終わった押下を次の Stable へ持ち越さないためである。
        /// 公開値はレベルなので、次の Stable ではその時点の量を読む。
        /// </summary>
        public void Sample(IInputDeviceReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (!InputAcceptance.IsAccepting(_interactionState))
            {
                InputPublication.WriteNeutral(_slots, _published);
                return;
            }

            reader.Read(_sampled);
            InputPublication.KeepActiveMap(_activeMap, _slots, _sampled);
            if (!InputPublication.TryCopy(_sampled, _published))
            {
                InputPublication.WriteNeutral(_slots, _published);
            }
        }
    }
}
