#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 呼び出し側が渡した InputActionAsset の Player / UI を読み、片方だけを有効にする。
    /// アセットの生成と破棄は呼び出し側のままである。この型は Destroy しない。
    /// expectedControlType が Button でも Vector2 でもないアクションは席にせず、件数だけ残す。
    /// 既存アセットの TrackedDevicePosition / Orientation がそれに当たる。
    /// </summary>
    internal sealed class InputActionAssetReader : IInputDeviceReader, IInputMapControl
    {
        private readonly InputActionMap _playerMap;
        private readonly InputActionMap _uiMap;
        private readonly InputAction[] _actions;
        private readonly InputActionSlot[] _slots;

        public InputActionAssetReader(InputActionAsset asset)
        {
            // InputActionAsset は UnityEngine.Object なので、破棄済みを言語 null と取り違えない。
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            _playerMap = RequireMap(asset, InputActionMapNames.Player);
            _uiMap = RequireMap(asset, InputActionMapNames.UI);

            var actions = new List<InputAction>();
            var slots = new List<InputActionSlot>();
            var skipped = 0;
            Append(_playerMap, InputControlMap.Player, actions, slots, ref skipped);
            Append(_uiMap, InputControlMap.UI, actions, slots, ref skipped);
            _actions = actions.ToArray();
            _slots = slots.ToArray();
            UnsupportedActionCount = skipped;
            Apply(InputControlMap.Player);
        }

        public int UnsupportedActionCount { get; }

        public InputActionSlot[] Slots => _slots;

        public void Read(Span<InputActionValue> destination)
        {
            var count = _actions.Length < destination.Length ? _actions.Length : destination.Length;
            for (var i = 0; i < count; i++)
            {
                var slot = _slots[i];
                var action = _actions[i];
                if (slot.Kind == InputActionValueKind.Button)
                {
                    var pressed = action.ReadValue<float>();
                    destination[i] = new InputActionValue(slot.Index, slot.Map, slot.Kind, pressed, 0f);
                    continue;
                }

                var axis = action.ReadValue<Vector2>();
                destination[i] = new InputActionValue(slot.Index, slot.Map, slot.Kind, axis.x, axis.y);
            }
        }

        public void Apply(InputControlMap map)
        {
            // 両方有効にしない。先に無効化してから、選んだマップだけを有効にする。
            if (map == InputControlMap.UI)
            {
                _playerMap.Disable();
                _uiMap.Enable();
                return;
            }

            _uiMap.Disable();
            _playerMap.Enable();
        }

        public void Disable()
        {
            _playerMap.Disable();
            _uiMap.Disable();
        }

        private static InputActionMap RequireMap(InputActionAsset asset, string mapName)
        {
            // InputActionMap は UnityEngine.Object ではない。言語 null が欠落である。
            var map = asset.FindActionMap(mapName, throwIfNotFound: false);
            if (map == null)
            {
                throw new InvalidOperationException(
                    $"InputActionAsset に {mapName} アクションマップが無い。");
            }

            return map;
        }

        private static void Append(
            InputActionMap map,
            InputControlMap controlMap,
            List<InputAction> actions,
            List<InputActionSlot> slots,
            ref int skipped)
        {
            var defined = map.actions;
            for (var i = 0; i < defined.Count; i++)
            {
                var action = defined[i];
                if (action == null)
                {
                    throw new InvalidOperationException($"{controlMap} マップの {i} 番目のアクションが空です。");
                }

                if (!InputActionKindMap.TryGetKind(action.expectedControlType, out var kind))
                {
                    skipped++;
                    continue;
                }

                var name = action.name;
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidOperationException($"{controlMap} マップに名前の無いアクションがあります。");
                }

                var index = slots.Count;
                slots.Add(new InputActionSlot(index, controlMap, kind, name));
                actions.Add(action);
            }
        }
    }
}
