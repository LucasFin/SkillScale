using System;
using HarmonyLib;

namespace SkillScale.Patches
{
    [HarmonyPatch(typeof(Skills), nameof(Skills.Awake))]
    internal static class SkillsAwakePatch
    {
        private static void Postfix(Skills __instance)
        {
            try
            {
                ModConfig.DiscoverFromSkills(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Skill discovery failed: {ex.Message}");
            }
        }
    }
}
