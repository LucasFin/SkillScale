using System;
using HarmonyLib;

namespace SkillScale.Patches
{
    [HarmonyPatch(typeof(Skills), nameof(Skills.LowerAllSkills))]
    internal static class DeathPenaltyPatch
    {
        private static void Prefix(ref float factor)
        {
            try
            {
                factor = Utilities.ApplyModifierValue(factor, ModConfig.DeathPenaltyMultiplier.Value);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Death penalty prefix failed: {ex.Message}");
            }
        }
    }
}
