using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace StoreManager
{
    public enum GameKey
    {
        W, A, S, D, Q, E, R, Tab, Space, Escape, Alpha1, Alpha2, Alpha3, Alpha4, F5, F9
    }

    /// <summary>
    /// Works with either the legacy Input Manager or the new Input System package,
    /// whichever the project has active.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static Key Map(GameKey k)
        {
            switch (k)
            {
                case GameKey.W: return Key.W;
                case GameKey.A: return Key.A;
                case GameKey.S: return Key.S;
                case GameKey.D: return Key.D;
                case GameKey.Q: return Key.Q;
                case GameKey.E: return Key.E;
                case GameKey.R: return Key.R;
                case GameKey.Tab: return Key.Tab;
                case GameKey.Space: return Key.Space;
                case GameKey.Escape: return Key.Escape;
                case GameKey.Alpha1: return Key.Digit1;
                case GameKey.Alpha2: return Key.Digit2;
                case GameKey.Alpha3: return Key.Digit3;
                case GameKey.F5: return Key.F5;
                case GameKey.F9: return Key.F9;
                default: return Key.Digit4;
            }
        }

        public static bool Held(GameKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
        }

        public static bool Down(GameKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
        }

        public static Vector2 MousePosition
        {
            get { return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero; }
        }

        public static bool MouseDown(int button)
        {
            var m = Mouse.current;
            if (m == null) return false;
            if (button == 0) return m.leftButton.wasPressedThisFrame;
            if (button == 1) return m.rightButton.wasPressedThisFrame;
            return m.middleButton.wasPressedThisFrame;
        }

        public static float Scroll
        {
            get { return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f; }
        }
#else
        static KeyCode Map(GameKey k)
        {
            switch (k)
            {
                case GameKey.W: return KeyCode.W;
                case GameKey.A: return KeyCode.A;
                case GameKey.S: return KeyCode.S;
                case GameKey.D: return KeyCode.D;
                case GameKey.Q: return KeyCode.Q;
                case GameKey.E: return KeyCode.E;
                case GameKey.R: return KeyCode.R;
                case GameKey.Tab: return KeyCode.Tab;
                case GameKey.Space: return KeyCode.Space;
                case GameKey.Escape: return KeyCode.Escape;
                case GameKey.Alpha1: return KeyCode.Alpha1;
                case GameKey.Alpha2: return KeyCode.Alpha2;
                case GameKey.Alpha3: return KeyCode.Alpha3;
                case GameKey.F5: return KeyCode.F5;
                case GameKey.F9: return KeyCode.F9;
                default: return KeyCode.Alpha4;
            }
        }

        public static bool Held(GameKey k) { return Input.GetKey(Map(k)); }
        public static bool Down(GameKey k) { return Input.GetKeyDown(Map(k)); }
        public static Vector2 MousePosition { get { return Input.mousePosition; } }
        public static bool MouseDown(int button) { return Input.GetMouseButtonDown(button); }
        public static float Scroll { get { return Input.mouseScrollDelta.y; } }
#endif
    }
}
