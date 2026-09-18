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
        /// <summary>V, and F7 as a spare: letter keys do not reach the game under UI automation, so the view can still be switched in tests.</summary>
        public static bool ToggleView => LegacyDown(KeyCode.V) || LegacyDown(KeyCode.F7)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.V) || Down(Key.F7)
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
        /// <summary>F4 on Elbrus: step to the next point of the summit route, kitted out and acclimatised. Testing the top of a nine-hour climb by
        /// walking to it is not testing, and the letter keys do not reach the game under automation, so this sits on an
        /// F-key. It does nothing on the pass.</summary>
        public static bool JumpRoute => Pressed(KeyCode.F4
#if ENABLE_INPUT_SYSTEM
            , Key.F4
#endif
            );

        /// <summary>F4: watch the Menk (camera beside it). F5 (host): wake it now.</summary>
        public static bool MenkWatch => LegacyDown(KeyCode.F4)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F4)
#endif
            ;
        /// <summary>F6 (host): the night clock jumps a minute ahead (testing the sky).</summary>
        public static bool SkipMinute => LegacyDown(KeyCode.F6)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F6)
#endif
            ;
        /// <summary>F8 / F9: turn the view 15° left or right. Mouse look does not reach the game under UI automation, so checks need this.</summary>
        public static bool TurnLeft => LegacyDown(KeyCode.F8)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F8)
#endif
            ;
        public static bool TurnRight => LegacyDown(KeyCode.F9)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F9)
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
        /// <summary>Эльбрус: сесть в кабину, кресло или ратрак и выйти из них. E, дубль F10 (буквы не доходят при автоматизации).</summary>
        public static bool Board => LegacyDown(KeyCode.E) || LegacyDown(KeyCode.F10)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.E) || Down(Key.F10)
#endif
            ;
        /// <summary>Эльбрус: ускорить время, пока едешь (F11; F8 и F9 крутят взгляд).</summary>
        public static bool TimeWarp => LegacyDown(KeyCode.F11) || LegacyDown(KeyCode.Alpha0) || LegacyDown(KeyCode.Keypad0)
#if ENABLE_INPUT_SYSTEM
            || Down(Key.F11) || Down(Key.Digit0) || Down(Key.Numpad0)
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
        /// <summary>Tab: open the rucksack (own, one in the snow or a companion's).</summary>
        public static bool PackOpen => Pressed(KeyCode.Tab
#if ENABLE_INPUT_SYSTEM
            , Key.Tab
#endif
            );
        /// <summary>G: take the rucksack off / put it on.</summary>
        public static bool PackWear => Pressed(KeyCode.G
#if ENABLE_INPUT_SYSTEM
            , Key.G
#endif
            );
        /// <summary>R: pick up what lies in front / stow what is in the hands.</summary>
        public static bool PackGrab => Pressed(KeyCode.R
#if ENABLE_INPUT_SYSTEM
            , Key.R
#endif
            );
        /// <summary>X: drop what is in the hands into the snow.</summary>
        public static bool PackDropHand => Pressed(KeyCode.X
#if ENABLE_INPUT_SYSTEM
            , Key.X
#endif
            );

        public static bool MiniMap => Pressed(KeyCode.N
#if ENABLE_INPUT_SYSTEM
            , Key.N
#endif
            );

        // ---- the ascent of Elbrus --------------------------------------------
        // F1…F3 belong to the Kholat night (travel, archive, site views) and do nothing on the southern slope, so the
        // two things a climber does with his hands take them as spares: letter keys never reach the game under UI
        // automation, and both also have a button in the HUD.

        /// <summary>C — put the crampons on or take them off (12 s standing still). Spare: F1.</summary>
        public static bool Crampons => Pressed(KeyCode.C
#if ENABLE_INPUT_SYSTEM
            , Key.C
#endif
            ) || Pressed(KeyCode.F1
#if ENABLE_INPUT_SYSTEM
            , Key.F1
#endif
            );

        /// <summary>B — pitch the tent here, or strike the one standing in front of you. Spares: 7, Numpad7, and the
        /// two buttons in the rucksack window. Every function key is already taken by the night and the ropeways, so
        /// the camp takes the free digits instead — digits reach the game under UI automation exactly as F-keys do.</summary>
        public static bool CampToggle => Pressed(KeyCode.B
#if ENABLE_INPUT_SYSTEM
            , Key.B
#endif
            ) || Pressed(KeyCode.Alpha7
#if ENABLE_INPUT_SYSTEM
            , Key.Digit7
#endif
            ) || Pressed(KeyCode.Keypad7
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad7
#endif
            );

        /// <summary>P — spend the night in the camp: the sortie is closed, the body digests the height and the run is
        /// written to disk. Spares: 8, Numpad8, and a button in the rucksack window.</summary>
        public static bool CampSleep => Pressed(KeyCode.P
#if ENABLE_INPUT_SYSTEM
            , Key.P
#endif
            ) || Pressed(KeyCode.Alpha8
#if ENABLE_INPUT_SYSTEM
            , Key.Digit8
#endif
            ) || Pressed(KeyCode.Keypad8
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad8
#endif
            );

        /// <summary>9 — SOS to the ЭВПСО. Every function key is taken and every letter is unreliable under UI
        /// automation, so the call for help takes the one digit nothing else wanted, plus the button in the rucksack
        /// window. It works wherever the party is: a party that needs it is not standing at a counter.</summary>
        public static bool Sos => Pressed(KeyCode.Alpha9
#if ENABLE_INPUT_SYSTEM
            , Key.Digit9
#endif
            ) || Pressed(KeyCode.Keypad9
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad9
#endif
            );

        /// <summary>U — take the folded programme sheet out of the rucksack and unfold it, or fold it back.
        /// Spare: F3, the last function key the southern slope leaves free (F1…F3 belong to the Kholat night, and
        /// every digit is already taken), plus a button in the rucksack window.</summary>
        public static bool Sheet => Pressed(KeyCode.U
#if ENABLE_INPUT_SYSTEM
            , Key.U
#endif
            ) || Pressed(KeyCode.F3
#if ENABLE_INPUT_SYSTEM
            , Key.F3
#endif
            );

        /// <summary>T — a sip of hot out of the thermos. Spare: F2.</summary>
        public static bool Thermos => Pressed(KeyCode.T
#if ENABLE_INPUT_SYSTEM
            , Key.T
#endif
            ) || Pressed(KeyCode.F2
#if ENABLE_INPUT_SYSTEM
            , Key.F2
#endif
            );

        // ---- skis, poles and the волокуша ------------------------------------
        // Letter keys do not reach the game under UI automation, so every one of these has a digit and a keypad twin,
        // and the two free function keys (F1, F12) cover the whole set: F1 steps through all four ways of travelling,
        // F12 hitches and unhitches the sled. F2…F11 are taken (archive, sites, Menk, sky, view, turning, boarding, time).

        /// <summary>K — put the skis on or take them off (they ride on the pack when they are off). Spares: 4, Numpad4.</summary>
        public static bool SkiToggle => Pressed(KeyCode.K
#if ENABLE_INPUT_SYSTEM
            , Key.K
#endif
            ) || Pressed(KeyCode.Alpha4
#if ENABLE_INPUT_SYSTEM
            , Key.Digit4
#endif
            ) || Pressed(KeyCode.Keypad4
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad4
#endif
            );

        /// <summary>L — take the poles off the pack or lash them back on. Spares: 5, Numpad5.</summary>
        public static bool PoleToggle => Pressed(KeyCode.L
#if ENABLE_INPUT_SYSTEM
            , Key.L
#endif
            ) || Pressed(KeyCode.Alpha5
#if ENABLE_INPUT_SYSTEM
            , Key.Digit5
#endif
            ) || Pressed(KeyCode.Keypad5
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad5
#endif
            );

        /// <summary>H — hitch the rucksack to the spare pair and tow it, or take the harness off. Spares: 6, Numpad6, F12.</summary>
        public static bool HaulToggle => Pressed(KeyCode.H
#if ENABLE_INPUT_SYSTEM
            , Key.H
#endif
            ) || Pressed(KeyCode.Alpha6
#if ENABLE_INPUT_SYSTEM
            , Key.Digit6
#endif
            ) || Pressed(KeyCode.Keypad6
#if ENABLE_INPUT_SYSTEM
            , Key.Numpad6
#endif
            ) || Pressed(KeyCode.F12
#if ENABLE_INPUT_SYSTEM
            , Key.F12
#endif
            );

        /// <summary>J, and F1 as the spare: step to the next way of travelling (пешком → с палками → на лыжах → с волокушей).
        /// One key reaches every mode, which is what an automated check needs.</summary>
        public static bool TravelCycle => Pressed(KeyCode.J
#if ENABLE_INPUT_SYSTEM
            , Key.J
#endif
            ) || Pressed(KeyCode.F1
#if ENABLE_INPUT_SYSTEM
            , Key.F1
#endif
            );

        public static string Debug => $"{(Forward ? "W" : "-")}{(Left ? "A" : "-")}{(Back ? "S" : "-")}{(Right ? "D" : "-")}";
    }
}
