using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkillScale.Patches
{
    /// <summary>
    /// Valheim locks the cursor every LateUpdate in GameCamera.UpdateMouseCapture.
    /// A Postfix unlock fights that and flickers. Skip the method entirely while our menu is open,
    /// and treat the menu like inventory so PlayerController stops applying look/move.
    /// </summary>
    internal static class MenuInputPatches
    {
        private static readonly FieldInfo MouseCaptureField =
            AccessTools.Field(typeof(GameCamera), "m_mouseCapture");

        private static bool _savedMouseCapture;
        private static bool _hasSavedMouseCapture;

        internal static void OnMenuOpened()
        {
            GameCamera camera = GameCamera.instance;
            if (camera != null && MouseCaptureField != null)
            {
                _savedMouseCapture = (bool)MouseCaptureField.GetValue(camera);
                _hasSavedMouseCapture = true;
                MouseCaptureField.SetValue(camera, false);
            }

            ForceFreeCursor();
            Plugin.Log?.LogInfo("SkillScale menu open: free cursor + block look/move");
        }

        internal static void OnMenuClosed()
        {
            GameCamera camera = GameCamera.instance;
            if (camera != null && MouseCaptureField != null && _hasSavedMouseCapture)
            {
                MouseCaptureField.SetValue(camera, _savedMouseCapture);
            }

            _hasSavedMouseCapture = false;
        }

        internal static void ForceFreeCursor()
        {
            // Bypass ZCursor.Show()'s IsMouseActive gate (that gate is what causes the flicker).
            ZCursor.LockState = CursorLockMode.None;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static class UpdateMouseCapturePatch
        {
            private static bool Prefix()
            {
                if (!SkillScaleMenu.IsOpen)
                {
                    return true;
                }

                ForceFreeCursor();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "TakeInput", typeof(bool))]
        private static class PlayerControllerTakeInputPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (SkillScaleMenu.IsOpen)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerController), "InInventoryEtc")]
        private static class PlayerControllerInventoryPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (SkillScaleMenu.IsOpen)
                {
                    __result = true;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static class PlayerTakeInputPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (SkillScaleMenu.IsOpen)
                {
                    __result = false;
                }
            }
        }

        /// <summary>
        /// Enter opens chat in Valheim. While our panel is open, keep Enter for confirming typed rates.
        /// </summary>
        [HarmonyPatch(typeof(Chat), "Update")]
        private static class ChatUpdatePatch
        {
            private static bool Prefix()
            {
                return !SkillScaleMenu.IsOpen;
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown))]
        private static class ZInputGetButtonDownPatch
        {
            private static void Postfix(string name, ref bool __result)
            {
                if (!SkillScaleMenu.IsOpen || !__result || string.IsNullOrEmpty(name))
                {
                    return;
                }

                if (name.Equals("Chat", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("JoyChat", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Use", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("JoyUse", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Attack", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("SecondAttack", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Block", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Jump", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Crouch", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Run", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Hide", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("GPower", System.StringComparison.OrdinalIgnoreCase))
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButton))]
        private static class ZInputGetButtonPatch
        {
            private static void Postfix(string name, ref bool __result)
            {
                if (!SkillScaleMenu.IsOpen || !__result || string.IsNullOrEmpty(name))
                {
                    return;
                }

                if (name.Equals("Attack", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("SecondAttack", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Block", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Jump", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Crouch", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Run", System.StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Hide", System.StringComparison.OrdinalIgnoreCase))
                {
                    __result = false;
                }
            }
        }

        /// <summary>
        /// Stop camera zoom from mouse wheel while the panel is open.
        /// Menu scrolling still works via IMGUI ScrollWheel events.
        /// </summary>
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
        private static class ZInputGetMouseScrollWheelPatch
        {
            private static void Postfix(ref float __result)
            {
                if (SkillScaleMenu.IsOpen)
                {
                    __result = 0f;
                }
            }
        }

        [HarmonyPatch(typeof(Input), nameof(Input.GetAxis), typeof(string))]
        private static class UnityInputGetAxisPatch
        {
            private static void Postfix(string axisName, ref float __result)
            {
                if (!SkillScaleMenu.IsOpen || string.IsNullOrEmpty(axisName) || __result == 0f)
                {
                    return;
                }

                if (axisName.IndexOf("Scroll", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    axisName.IndexOf("Zoom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    __result = 0f;
                }
            }
        }
    }
}
