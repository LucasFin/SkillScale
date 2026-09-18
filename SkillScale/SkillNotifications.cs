using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkillScale
{
    internal static class SkillNotifications
    {
        private const float MinToastGap = 0.5f;
        private const float MaxToastGap = 10f;
        private const float DefaultToastGap = 2.5f;

        private static readonly MethodInfo GetSkillMethod = AccessTools.Method(
            typeof(Skills), "GetSkill", new[] { typeof(Skills.SkillType) });

        private static readonly Dictionary<Skills.SkillType, float> LastToastAt =
            new Dictionary<Skills.SkillType, float>();

        internal static void TryShow(Skills skills, Skills.SkillType skillType, float factor)
        {
            if (!ModConfig.SwingXpNotifications.Value)
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

            float gap = Mathf.Clamp(ModConfig.XpToastInterval.Value, MinToastGap, MaxToastGap);
            float now = Time.unscaledTime;
            if (LastToastAt.TryGetValue(skillType, out float last) && now - last < gap)
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

            LastToastAt[skillType] = now;

            string skillToken = skill.m_info.m_skill.ToString().ToLowerInvariant();
            float percent = skill.GetLevelPercentage();
            float gained = skill.m_info.m_increseStep * factor;
            float next = NextLevelRequirement(skill);
            string rateBit = string.Empty;
            if (ModConfig.EnableSkillScaling.Value)
            {
                float rate = ModConfig.GetXpMultiplier(skillType);
                rateBit = $" · {rate:0.##}x";
            }

            string body =
                $"$skill_{skillToken}: {percent:P1} (+{gained:0.##}){rateBit}\n[{Round(skill.m_accumulator)}/{Round(next)}]";
            int size = Mathf.Clamp(ModConfig.NotificationTextSize.Value, ModConfig.MinNoticeSize, ModConfig.MaxNoticeSize);
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

        internal static float ClampToastGap(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultToastGap;
            }

            return Mathf.Clamp(value, MinToastGap, MaxToastGap);
        }
    }
}
