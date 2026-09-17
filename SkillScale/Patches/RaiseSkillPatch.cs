using System;
using HarmonyLib;

namespace SkillScale.Patches
{
    [HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
    internal static class RaiseSkillPatch
    {
        private static void Prefix(ref Skills.SkillType skillType, ref float factor)
        {
            try
            {
                if (!ModConfig.ChangeSkills.Value)
                {
                    return;
                }

                if (ModConfig.TryGetGainModifier(skillType, out float modifier))
                {
                    factor = Utilities.ApplyModifierValue(factor, modifier);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Skill gain prefix failed for {skillType}: {ex.Message}");
            }
        }

        private static void Postfix(Skills __instance, Skills.SkillType skillType, float factor)
        {
            // Never throw here. Harvesting grants farming XP before the item is picked.
            // If a notification fails, the pick must still finish.
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
