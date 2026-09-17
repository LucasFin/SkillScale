using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkillScale
{
    internal static class SkillNotifications
    {
        private static readonly MethodInfo GetSkillMethod = AccessTools.Method(
            typeof(Skills), "GetSkill", new[] { typeof(Skills.SkillType) });

        internal static void TryShow(Skills skills, Skills.SkillType skillType, float factor)
        {
            if (!ModConfig.ExperienceGainedNotifications.Value)
            {
                return;
            }

            if (skillType == Skills.SkillType.None || skillType == Skills.SkillType.All)
            {
                return;
            }

            if (ModConfig.SkipRunNotifications.Value && skillType == Skills.SkillType.Run)
            {
                return;
            }

            if (skills == null || GetSkillMethod == null)
            {
                return;
            }

            var skill = GetSkillMethod.Invoke(skills, new object[] { skillType }) as Skills.Skill;
            if (skill?.m_info == null || skill.m_level >= 100f)
            {
                return;
            }

            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            string skillToken = skill.m_info.m_skill.ToString().ToLowerInvariant();
            float percent = skill.GetLevelPercentage();
            float gained = skill.m_info.m_increseStep * factor;
            float next = NextLevelRequirement(skill);
            string body =
                $"$skill_{skillToken}: {percent:P2} (+{gained:0.##})\n[{Round(skill.m_accumulator)}/{Round(next)}]";
            int size = Mathf.Clamp(ModConfig.NotificationTextSize.Value, 8, 48);
            player.Message(MessageHud.MessageType.TopLeft, $"<size={size}>{body}</size>", 0, skill.m_info.m_icon);
        }

        private static float NextLevelRequirement(Skills.Skill skill)
        {
            return Mathf.Pow(Mathf.Floor(skill.m_level + 1f), 1.5f) * 0.5f + 0.5f;
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value * 100f) / 100f;
        }
    }
}
