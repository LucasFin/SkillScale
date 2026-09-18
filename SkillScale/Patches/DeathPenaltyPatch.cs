using System;
using HarmonyLib;

namespace SkillScale.Patches
{
    [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
    [HarmonyPriority(Priority.Last)]
    internal static class DeathPenaltyPatch
    {
        private static void Prefix(ref float factor)
        {
            try
            {
                factor = Utilities.Scale(factor, ModConfig.ClampDeathRate(ModConfig.DeathLossMultiplier.Value));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Death penalty prefix failed: {ex.Message}");
            }
        }
    }
}
