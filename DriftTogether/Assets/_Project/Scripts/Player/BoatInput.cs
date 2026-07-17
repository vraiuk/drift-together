using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftTogether.Player
{
    /// <summary>
    /// Reads keyboard + gamepad through the Input System and exposes a simple
    /// arcade input state. Also supports an autopilot override used by the
    /// automated smoke test.
    /// </summary>
    public sealed class BoatInput : MonoBehaviour
    {
        public float Thrust { get; private set; }   // -1..1 (S..W)
        public float Steer { get; private set; }    // -1..1 (A..D)
        public bool InteractPressed { get; private set; }
        public bool ResetPressed { get; private set; }
        public bool PausePressed { get; private set; }

        public bool AutopilotEnabled { get; set; }
        public float AutopilotThrust { get; set; }
        public float AutopilotSteer { get; set; }
        public bool AutopilotInteract { get; set; }

        void Update()
        {
            InteractPressed = false;
            ResetPressed = false;
            PausePressed = false;

            if (AutopilotEnabled)
            {
                Thrust = Mathf.Clamp(AutopilotThrust, -1f, 1f);
                Steer = Mathf.Clamp(AutopilotSteer, -1f, 1f);
                if (AutopilotInteract)
                {
                    InteractPressed = true;
                    AutopilotInteract = false;
                }
                return;
            }

            float thrust = 0f;
            float steer = 0f;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) thrust += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) thrust -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                if (kb.eKey.wasPressedThisFrame) InteractPressed = true;
                if (kb.rKey.wasPressedThisFrame) ResetPressed = true;
                if (kb.escapeKey.wasPressedThisFrame) PausePressed = true;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (Mathf.Abs(stick.y) > 0.15f) thrust += stick.y;
                if (Mathf.Abs(stick.x) > 0.15f) steer += stick.x;
                if (pad.buttonSouth.wasPressedThisFrame) InteractPressed = true;
                if (pad.buttonEast.wasPressedThisFrame) ResetPressed = true;
                if (pad.startButton.wasPressedThisFrame) PausePressed = true;
            }

            Thrust = Mathf.Clamp(thrust, -1f, 1f);
            Steer = Mathf.Clamp(steer, -1f, 1f);
        }
    }
}
