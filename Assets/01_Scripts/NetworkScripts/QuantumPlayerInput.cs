using Photon.Deterministic;
using UnityEngine.InputSystem;

namespace FlipFriends.Network
{
    public static class QuantumPlayerInput
    {
        public static Quantum.Input Read(int localSlot)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return default;
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
