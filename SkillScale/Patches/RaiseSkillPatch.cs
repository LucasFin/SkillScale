using System;
using HarmonyLib;

namespace SkillScale.Patches
{
    /// <summary>
    /// Priority.Last so we multiply whatever factor other mods left, instead of fighting them.
    /// </summary>
    [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
    [HarmonyPriority(Priority.Last)]
    internal static class RaiseSkillPatch
    {
        private static void Prefix(ref Skills.SkillType skillType, ref float factor)
        {
            try
            {
                if (!ModConfig.EnableSkillScaling.Value)
                {
                    return;
                }

                if (skillType == Skills.SkillType.None || skillType == Skills.SkillType.All)
                {
                    return;
                }

                ModConfig.EnsureSkillMultiplier(skillType);
                factor = Utilities.Scale(factor, ModConfig.GetXpMultiplier(skillType));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Skill gain prefix failed for {skillType}: {ex.Message}");
            }
        }

        private static void Postfix(Skills __instance, Skills.SkillType skillType, float factor)
        {
            // Never throw. Harvesting grants farming XP before the pick finishes.
            try
            {
                SkillNotifications.TryShow(__instance, skillType, factor);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Skill notification failed for {skillType}: {ex.Message}");
            }
        }
    }
}
