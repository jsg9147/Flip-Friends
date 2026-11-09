using System;
using Photon.Deterministic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlipFriends.Network
{
    public sealed class QuantumPlayerInput : IDisposable
    {
        private readonly InputActionAsset[] actions;
        private readonly Gamepad[] pads;
        private readonly int keyboardCount;
        private Keyboard keyboard;
        private readonly string savedBindings;
        private bool isDisposed;
        public static int ActiveDeviceSubscriptions { get; private set; }

        public QuantumPlayerInput(int slots, int keyboardCount)
        {
            this.keyboardCount = keyboardCount;
            actions = new InputActionAsset[slots];
            pads = new Gamepad[slots];
            savedBindings = PlayerPrefs.GetString("bindings", string.Empty);
            try { AssignDevices(); }
            catch
            {
                for (int slot = 0; slot < actions.Length; slot++) Release(slot);
                throw;
            }
            InputSystem.onDeviceChange += OnDeviceChange;
            ActiveDeviceSubscriptions++;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad || device is Keyboard) AssignDevices();
        }

        private void AssignDevices()
        {
            if (keyboardCount > 0 && (keyboard != Keyboard.current || actions[0] == null))
            {
                keyboard = Keyboard.current;
                SetDevice(0, keyboard);
            }
            for (int slot = keyboardCount; slot < pads.Length; slot++)
            {
                if (pads[slot] != null && pads[slot].added) continue;
                pads[slot] = null;
                foreach (var pad in Gamepad.all)
                {
                    if (!pad.added || Array.IndexOf(pads, pad) >= 0) continue;
                    pads[slot] = pad;
                    break;
                }
                SetDevice(slot, pads[slot]);
            }
        }

        private void SetDevice(int slot, InputDevice device)
        {
            Release(slot);
            if (device == null || InputSystem.actions == null) return;
            var asset = UnityEngine.Object.Instantiate(InputSystem.actions);
            actions[slot] = asset;
            asset.Disable();
            if (!string.IsNullOrEmpty(savedBindings))
            {
                try { asset.LoadBindingOverridesFromJson(savedBindings); }
                catch (Exception error) { Debug.LogWarning("[Quantum] 키 설정을 읽지 못했습니다. " + error.Message); }
            }
            var map = asset.FindActionMap("Player", true);
            if (device is Keyboard)
            {
                var move = map.FindAction("Move", true);
                string[] defaultKeys = { "w", "s", "a", "d" };
                for (int i = 0; i < defaultKeys.Length; i++)
                    if (move.bindings[i + 1].overridePath == null)
                        move.ApplyBindingOverride(i + 1, "<Keyboard>/" + defaultKeys[i]);
            }
            asset.devices = new[] { device };
            map.Enable();
        }

        public Quantum.Input ReadSlot(int slot)
        {
            if (slot < 0 || slot >= actions.Length) return default;
            var asset = actions[slot];
            if (asset == null) return slot < keyboardCount ? ReadKeyboard(keyboard, slot) : default;
            var map = asset.FindActionMap("Player", true);
            var move = map.FindAction("Move", true).ReadValue<Vector2>();
            if (slot < keyboardCount) move = new Vector2(Math.Sign(move.x), Math.Sign(move.y));
            return new Quantum.Input
            {
                Horizontal = FP.FromFloat_UNSAFE(move.x),
                Vertical = FP.FromFloat_UNSAFE(move.y),
                Jump = map.FindAction("Jump", true).IsPressed(),
                Run = map.FindAction("Sprint", true).IsPressed(),
                Interact = map.FindAction("Interact", true).IsPressed(),
                Reset = map.FindAction("Reset", true).IsPressed()
            };
        }

        private void Release(int slot)
        {
            if (actions[slot] == null) return;
            actions[slot].Disable();
            UnityEngine.Object.Destroy(actions[slot]);
            actions[slot] = null;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            InputSystem.onDeviceChange -= OnDeviceChange;
            ActiveDeviceSubscriptions--;
            for (int slot = 0; slot < actions.Length; slot++) Release(slot);
        }

        public static Quantum.Input Read(int localSlot) => ReadKeyboard(Keyboard.current, localSlot);

        private static Quantum.Input ReadKeyboard(Keyboard keyboard, int localSlot)
        {
            if (keyboard == null || !keyboard.added || localSlot < 0 || localSlot > 1) return default;
            bool isSecond = localSlot == 1;
            return new Quantum.Input
            {
                Horizontal = ((isSecond ? keyboard.rightArrowKey.isPressed : keyboard.dKey.isPressed) ? 1 : 0) -
                    ((isSecond ? keyboard.leftArrowKey.isPressed : keyboard.aKey.isPressed) ? 1 : 0),
                Vertical = ((isSecond ? keyboard.upArrowKey.isPressed : keyboard.wKey.isPressed) ? 1 : 0) -
                    ((isSecond ? keyboard.downArrowKey.isPressed : keyboard.sKey.isPressed) ? 1 : 0),
                Jump = isSecond ? keyboard.rightCtrlKey.isPressed : keyboard.spaceKey.isPressed,
                Run = isSecond ? keyboard.rightShiftKey.isPressed : keyboard.leftShiftKey.isPressed,
                Interact = isSecond ? keyboard.enterKey.isPressed : keyboard.eKey.isPressed,
                Reset = isSecond ? keyboard.backspaceKey.isPressed : keyboard.rKey.isPressed
            };
        }

        public static Quantum.Input ReadScripted(int frame, int player) => new Quantum.Input
        {
            Horizontal = (frame / 120 + player) % 2 == 0 ? FP._1 : -FP._1,
            Jump = frame % 90 < 10
        };
    }
}
