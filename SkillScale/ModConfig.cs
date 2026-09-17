using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace SkillScale
{
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> ChangeSkills;
        internal static ConfigEntry<bool> ExperienceGainedNotifications;
        internal static ConfigEntry<bool> SkipRunNotifications;
        internal static ConfigEntry<int> NotificationTextSize;
        internal static ConfigEntry<float> DeathPenaltyMultiplier;

        internal static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> SkillGain =
            new Dictionary<Skills.SkillType, ConfigEntry<float>>();

        private static readonly Dictionary<Skills.SkillType, string> DisplayNames =
            new Dictionary<Skills.SkillType, string>
            {
                { Skills.SkillType.Swords, "Sword" },
                { Skills.SkillType.Knives, "Knives" },
                { Skills.SkillType.Clubs, "Clubs" },
                { Skills.SkillType.Polearms, "Polearm" },
                { Skills.SkillType.Spears, "Spear" },
                { Skills.SkillType.Blocking, "Block" },
                { Skills.SkillType.Axes, "Axe" },
                { Skills.SkillType.Bows, "Bow" },
                { Skills.SkillType.ElementalMagic, "Elemental Magic" },
                { Skills.SkillType.BloodMagic, "Blood Magic" },
                { Skills.SkillType.Unarmed, "Unarmed" },
                { Skills.SkillType.Pickaxes, "Pickaxe" },
                { Skills.SkillType.WoodCutting, "WoodCutting" },
                { Skills.SkillType.Crossbows, "Crossbow" },
                { Skills.SkillType.Jump, "Jump" },
                { Skills.SkillType.Sneak, "Sneak" },
                { Skills.SkillType.Run, "Run" },
                { Skills.SkillType.Swim, "Swim" },
                { Skills.SkillType.Fishing, "Fishing" },
                { Skills.SkillType.Cooking, "Cooking" },
                { Skills.SkillType.Farming, "Farming" },
                { Skills.SkillType.Crafting, "Crafting" },
                { Skills.SkillType.Dodge, "Dodge" },
                { Skills.SkillType.Ride, "Ride" }
            };

        internal static void Bind(Plugin plugin)
        {
            LockConfiguration = plugin.BindSynced(
                "1 - General", "Lock Configuration", true,
                "If on, only server admins can change the config.");
            Plugin.ConfigSync.AddLockingConfigEntry(LockConfiguration);

            ChangeSkills = plugin.BindSynced(
                "2 - Skills", "Change the skill gain factor", true,
                "Turn the skill gain settings below on or off.");

            ExperienceGainedNotifications = plugin.BindSynced(
                "2 - Skills", "Display notifications for skills gained", true,
                "Show a small message in the top left when a skill gains XP.");

            SkipRunNotifications = plugin.BindSynced(
                "2 - Skills", "Should running skill notifications be ignored", true,
                "Hide Run skill messages so they do not spam the screen.");

            NotificationTextSize = plugin.BindSynced(
                "2 - Skills", "Skill Notification Text Size", 14,
                "Size of the skill gain message text.");

            foreach (Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (skill == Skills.SkillType.None || skill == Skills.SkillType.All)
                {
                    continue;
                }

                string label = DisplayNames.TryGetValue(skill, out string pretty) ? pretty : skill.ToString();
                SkillGain[skill] = plugin.BindSynced(
                    "2 - Skills", $"{label} Skill gain factor", 0f,
                    $"{label} skill gain. 50 means +50% XP, -50 means -50% XP, 0 is normal.");
            }

            DeathPenaltyMultiplier = plugin.BindSynced(
                "2 - Skills", "Death Penalty Factor Multiplier", 0f,
                "How much skill you lose on death. 50 means +50% loss, -50 means -50% loss, 0 is normal.");
        }

        internal static bool TryGetGainModifier(Skills.SkillType skill, out float modifier)
        {
            if (SkillGain.TryGetValue(skill, out ConfigEntry<float> entry))
            {
                modifier = entry.Value;
                return true;
            }

            modifier = 0f;
            return false;
        }
    }
}
