using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Height1079.Runtime
{
    /// <summary>Layout-independent keyboard. The legacy Input manager on macOS resolves letter keys by the typed character,
    /// so with a Russian layout W/A/S/D/E/V never fire. The Input System reads physical key positions; the legacy path stays
    /// as a fallback (and arrows always work).</summary>
    public static class Controls
    {
#if ENABLE_INPUT_SYSTEM
        static Keyboard K => Keyboard.current;
        static bool Held(Key k) => K != null && K[k].isPressed;
        static bool Down(Key k) => K != null && K[k].wasPressedThisFrame;
#endif
        static bool LegacyHeld(KeyCode k) { try { return Input.GetKey(k); } catch (System.InvalidOperationException) { return false; } }
        static bool LegacyDown(KeyCode k) { try { return Input.GetKeyDown(k); } catch (System.InvalidOperationException) { return false; } }

        public static bool Forward => LegacyHeld(KeyCode.W) || LegacyHeld(KeyCode.UpArrow)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.W) || Held(Key.UpArrow)
#endif
            ;
        public static bool Back => LegacyHeld(KeyCode.S) || LegacyHeld(KeyCode.DownArrow)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.S) || Held(Key.DownArrow)
#endif
            ;
        public static bool Left => LegacyHeld(KeyCode.A) || LegacyHeld(KeyCode.LeftArrow)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.A) || Held(Key.LeftArrow)
#endif
            ;
        public static bool Right => LegacyHeld(KeyCode.D) || LegacyHeld(KeyCode.RightArrow)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.D) || Held(Key.RightArrow)
#endif
            ;
        public static bool Run => LegacyHeld(KeyCode.LeftShift) || LegacyHeld(KeyCode.RightShift)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.LeftShift) || Held(Key.RightShift)
#endif
            ;
        public static bool Kindle => LegacyHeld(KeyCode.E)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.E)
#endif
            ;
        public static bool ToggleView => LegacyDown(KeyCode.V)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.V)
#endif
            ;
        public static bool Pause => LegacyDown(KeyCode.Escape)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.Escape)
#endif
            ;
        public static bool Archive => LegacyDown(KeyCode.F2)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F2)
#endif
            ;
        /// <summary>F4: watch the Menk (camera beside it). F5 (host): wake it now.</summary>
        public static bool MenkWatch => LegacyDown(KeyCode.F4)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F4)
#endif
            ;
        public static bool MenkWake => LegacyDown(KeyCode.F5)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F5)
#endif
            ;
        public static bool SiteView => LegacyDown(KeyCode.F3)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F3)
#endif
            ;

        static bool Pressed(KeyCode legacy
#if ENABLE_INPUT_SYSTEM
            , Key key
#endif
            ) => LegacyDown(legacy)
#if ENABLE_INPUT_SYSTEM
            || Down(key)
#endif
            ;

        public static bool ItemCompass => Pressed(KeyCode.Alpha1
#if ENABLE_INPUT_SYSTEM
            , Key.Digit1
#endif
            );
        public static bool ItemTorch => Pressed(KeyCode.Alpha2
#if ENABLE_INPUT_SYSTEM
            , Key.Digit2
#endif
            );
        public static bool ItemMap => Pressed(KeyCode.Alpha3
#if ENABLE_INPUT_SYSTEM
            , Key.Digit3
#endif
            ) || Pressed(KeyCode.M
#if ENABLE_INPUT_SYSTEM
            , Key.M
#endif
            );
        public static bool ItemNone => Pressed(KeyCode.Alpha0
#if ENABLE_INPUT_SYSTEM
            , Key.Digit0
#endif
            ) || Pressed(KeyCode.Q
#if ENABLE_INPUT_SYSTEM
            , Key.Q
#endif
            );
        public static bool TorchSwitch => Pressed(KeyCode.F
#if ENABLE_INPUT_SYSTEM
            , Key.F
#endif
            );
        /// <summary>F held — squeezing the lever of the dynamo "жучок".</summary>
        public static bool TorchHold => LegacyHeld(KeyCode.F)
#if ENABLE_INPUT_SYSTEM
            || Held(Key.F)
#endif
            ;
        public static bool MiniMap => Pressed(KeyCode.N
#if ENABLE_INPUT_SYSTEM
            , Key.N
#endif
            );

        public static string Debug => $"{(Forward ? "W" : "-")}{(Left ? "A" : "-")}{(Back ? "S" : "-")}{(Right ? "D" : "-")}";
    }
}
