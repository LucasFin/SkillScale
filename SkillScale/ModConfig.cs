using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace SkillScale
{
    internal static class ModConfig
    {
        internal const float MinRate = 0f;
        internal const float MaxXpRate = 10f;
        internal const float MaxDeathRate = 5f;
        internal const int MinNoticeSize = 8;
        internal const int MaxNoticeSize = 48;

        private const string SectionGeneral = "1 - General";
        private const string SectionRates = "2 - Rates";
        private const string SectionSkills = "3 - Per Skill";
        private const string SectionDeath = "4 - Death";
        private const string SectionNotifs = "5 - Notifications";
        private const string SectionMenu = "6 - In-Game Menu";

        internal static ConfigEntry<bool> LockConfiguration;
        internal static ConfigEntry<bool> EnableSkillScaling;
        internal static ConfigEntry<EmploymentPreset> Preset;
        internal static ConfigEntry<float> GlobalMultiplier;
        internal static ConfigEntry<float> DeathLossMultiplier;
        internal static ConfigEntry<bool> SwingXpNotifications;
        internal static ConfigEntry<bool> SkipRunNotifications;
        internal static ConfigEntry<float> XpToastInterval;
        internal static ConfigEntry<int> NotificationTextSize;
        internal static ConfigEntry<bool> EnableInGameMenu;
        internal static ConfigEntry<KeyboardShortcut> MenuKey;

        internal static readonly Dictionary<Skills.SkillType, ConfigEntry<float>> SkillMultipliers =
            new Dictionary<Skills.SkillType, ConfigEntry<float>>();

        internal static readonly Dictionary<Skills.SkillType, Sprite> SkillIcons =
            new Dictionary<Skills.SkillType, Sprite>();

        private static Plugin _plugin;
        private static bool _applyingPreset;
        private static bool _applyingGlobalFromPreset;
        private static bool _sanitizing;
        private static float _lastGlobal = 1f;

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
            _plugin = plugin;

            LockConfiguration = plugin.BindSynced(
                SectionGeneral, "Lock Configuration", true,
                "If on, only server admins can change the config.");
            Plugin.ConfigSync.AddLockingConfigEntry(LockConfiguration);

            EnableSkillScaling = plugin.BindSynced(
                SectionGeneral, "Enable Skill Scaling", true,
                "Master switch for XP rate changes. Off = vanilla skill gain.");

            Preset = plugin.BindSynced(
                SectionRates, "Employment Preset", EmploymentPreset.Unemployed,
                "Giga Unemployed 0.75x, Unemployed 1x (default), Part-time 1.5x, Full-time 2.5x. Custom means you set the global dial yourself.");

            GlobalMultiplier = plugin.BindSynced(
                SectionRates, "Global Multiplier", 1f,
                new ConfigDescription(
                    "XP rate applied to every skill that still matches this value. Change a skill under Per Skill to give it its own rate (not multiplied). Allowed 0 to 10.",
                    new AcceptableValueRange<float>(MinRate, MaxXpRate)));

            DeathLossMultiplier = plugin.BindSynced(
                SectionDeath, "Death Skill Loss Multiplier", 1f,
                new ConfigDescription(
                    "How hard death hits your skills. 0 = keep skills, 1 = normal Valheim loss, 2 = twice as harsh. Allowed 0 to 5.",
                    new AcceptableValueRange<float>(MinRate, MaxDeathRate)));

            SwingXpNotifications = plugin.BindSynced(
                SectionNotifs, "Show XP On Action", true,
                "Top-left XP progress toasts while skills gain. Throttled so it stays readable. Vanilla level-up toasts still show.");

            SkipRunNotifications = plugin.BindSynced(
                SectionNotifs, "Skip Run XP Messages", true,
                "Hide Run when XP toasts are on (Run fires constantly).");

            XpToastInterval = plugin.BindSynced(
                SectionNotifs, "XP Toast Interval", 2.5f,
                new ConfigDescription(
                    "Seconds between toasts for the same skill. Lower = more messages.",
                    new AcceptableValueRange<float>(0.5f, 10f)));

            NotificationTextSize = plugin.BindSynced(
                SectionNotifs, "XP Message Text Size", 14,
                new ConfigDescription(
                    "Text size for the optional per-action XP message.",
                    new AcceptableValueRange<int>(MinNoticeSize, MaxNoticeSize)));

            EnableInGameMenu = plugin.BindSynced(
                SectionMenu, "Enable In-Game Menu", true,
                "Show a small SkillScale panel in-game. Uses its own hotkey, not F1.",
                synchronizedSetting: false);

            MenuKey = plugin.BindSynced(
                SectionMenu, "Toggle Hotkey", new KeyboardShortcut(KeyCode.Backslash),
                "Opens or closes the SkillScale panel. Default is backslash (\\ or | key). Ignored while chat, console, or text prompts have focus.",
                synchronizedSetting: false);

            foreach (Skills.SkillType skill in Enum.GetValues(typeof(Skills.SkillType)))
            {
                if (skill == Skills.SkillType.None || skill == Skills.SkillType.All)
                {
                    continue;
                }

                EnsureSkillMultiplier(skill, GetDisplayName(skill));
            }

            SanitizeRates();
            _lastGlobal = ClampXpRate(GlobalMultiplier.Value);
            Preset.SettingChanged += OnPresetChanged;
            GlobalMultiplier.SettingChanged += OnGlobalChanged;
            plugin.Config.ConfigReloaded += (_, __) => SanitizeRates();
        }

        internal static float ClampXpRate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }

            return Mathf.Clamp(value, MinRate, MaxXpRate);
        }

        internal static float ClampDeathRate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 1f;
            }

            return Mathf.Clamp(value, MinRate, MaxDeathRate);
        }

        /// <summary>
        /// Pull wild .cfg edits back into safe ranges so sliders and gameplay stay sane.
        /// </summary>
        internal static void SanitizeRates()
        {
            if (_sanitizing || GlobalMultiplier == null)
            {
                return;
            }

            _sanitizing = true;
            try
            {
                float global = ClampXpRate(GlobalMultiplier.Value);
                if (!Approximately(GlobalMultiplier.Value, global))
                {
                    GlobalMultiplier.Value = global;
                }

                if (DeathLossMultiplier != null)
                {
                    float death = ClampDeathRate(DeathLossMultiplier.Value);
                    if (!Approximately(DeathLossMultiplier.Value, death))
                    {
                        DeathLossMultiplier.Value = death;
                    }
                }

                if (NotificationTextSize != null)
                {
                    int size = Mathf.Clamp(NotificationTextSize.Value, MinNoticeSize, MaxNoticeSize);
                    if (NotificationTextSize.Value != size)
                    {
                        NotificationTextSize.Value = size;
                    }
                }

                if (XpToastInterval != null)
                {
                    float gap = SkillNotifications.ClampToastGap(XpToastInterval.Value);
                    if (!Approximately(XpToastInterval.Value, gap))
                    {
                        XpToastInterval.Value = gap;
                    }
                }

                foreach (ConfigEntry<float> entry in SkillMultipliers.Values)
                {
                    float rate = ClampXpRate(entry.Value);
                    if (!Approximately(entry.Value, rate))
                    {
                        entry.Value = rate;
                    }
                }

                _lastGlobal = ClampXpRate(GlobalMultiplier.Value);
            }
            finally
            {
                _sanitizing = false;
            }
        }

        internal static float PresetRate(EmploymentPreset preset)
        {
            switch (preset)
            {
                case EmploymentPreset.GigaUnemployed:
                    return 0.75f;
                case EmploymentPreset.Unemployed:
                    return 1f;
                case EmploymentPreset.PartTime:
                    return 1.5f;
                case EmploymentPreset.FullTime:
                    return 2.5f;
                default:
                    return GlobalMultiplier != null ? ClampXpRate(GlobalMultiplier.Value) : 1f;
            }
        }

        internal static void EnsureSkillMultiplier(Skills.SkillType skill, string displayName = null)
        {
            if (skill == Skills.SkillType.None || skill == Skills.SkillType.All)
            {
                return;
            }

            if (SkillMultipliers.ContainsKey(skill))
            {
                return;
            }

            if (_plugin == null)
            {
                return;
            }

            string label = string.IsNullOrEmpty(displayName) ? GetDisplayName(skill) : displayName;
            float starting = GlobalMultiplier != null ? ClampXpRate(GlobalMultiplier.Value) : 1f;
            // DefaultValue stays 1 (vanilla). New skills start at the current global so they follow it.
            ConfigEntry<float> entry = _plugin.BindSynced(
                SectionSkills, $"{label} Multiplier", 1f,
                new ConfigDescription(
                    $"{label} XP rate (final for this skill). Not multiplied by global. Allowed 0 to 10.",
                    new AcceptableValueRange<float>(MinRate, MaxXpRate)));
            if (Approximately(entry.Value, 1f) && !Approximately(starting, 1f))
            {
                entry.Value = starting;
            }

            SkillMultipliers[skill] = entry;
        }

        internal static void DiscoverFromSkills(Skills skills)
        {
            if (skills?.m_skills == null)
            {
                return;
            }

            foreach (Skills.SkillDef def in skills.m_skills)
            {
                if (def == null)
                {
                    continue;
                }

                EnsureSkillMultiplier(def.m_skill, GetDisplayName(def.m_skill));
                if (def.m_icon != null)
                {
                    SkillIcons[def.m_skill] = def.m_icon;
                }
            }
        }

        /// <summary>
        /// Final XP rate for this skill. Per-skill values are absolute, not stacked on global.
        /// </summary>
        internal static float GetXpMultiplier(Skills.SkillType skill)
        {
            if (SkillMultipliers.TryGetValue(skill, out ConfigEntry<float> entry))
            {
                return ClampXpRate(entry.Value);
            }

            return ClampXpRate(GlobalMultiplier.Value);
        }

        internal static void SetAllSkillRates(float rate)
        {
            float clamped = ClampXpRate(rate);
            foreach (ConfigEntry<float> entry in SkillMultipliers.Values)
            {
                if (!Approximately(entry.Value, clamped))
                {
                    entry.Value = clamped;
                }
            }
        }

        internal static string GetDisplayName(Skills.SkillType skill)
        {
            if (DisplayNames.TryGetValue(skill, out string pretty))
            {
                return pretty;
            }

            string raw = skill.ToString();
            if (string.IsNullOrEmpty(raw) || raw.StartsWith("Unknown", StringComparison.Ordinal))
            {
                return $"Skill {(int)skill}";
            }

            return raw;
        }

        private static void OnPresetChanged(object sender, EventArgs e)
        {
            if (_applyingGlobalFromPreset)
            {
                return;
            }

            ApplyPresetToGlobal(Preset.Value, force: true);
        }

        private static void OnGlobalChanged(object sender, EventArgs e)
        {
            if (_sanitizing)
            {
                return;
            }

            float neu = ClampXpRate(GlobalMultiplier.Value);
            if (!Approximately(GlobalMultiplier.Value, neu))
            {
                GlobalMultiplier.Value = neu;
                return;
            }

            float old = _lastGlobal;

            if (!_applyingPreset && !Approximately(old, neu))
            {
                foreach (ConfigEntry<float> entry in SkillMultipliers.Values)
                {
                    if (Approximately(entry.Value, old))
                    {
                        entry.Value = neu;
                    }
                }
            }

            _lastGlobal = neu;

            if (_applyingPreset)
            {
                return;
            }

            EmploymentPreset match = MatchPreset(neu);
            if (Preset.Value != match)
            {
                _applyingGlobalFromPreset = true;
                try
                {
                    Preset.Value = match;
                }
                finally
                {
                    _applyingGlobalFromPreset = false;
                }
            }
        }

        private static void ApplyPresetToGlobal(EmploymentPreset preset, bool force)
        {
            if (preset == EmploymentPreset.Custom)
            {
                return;
            }

            float rate = ClampXpRate(PresetRate(preset));
            if (!force && Approximately(GlobalMultiplier.Value, rate))
            {
                return;
            }

            _applyingPreset = true;
            try
            {
                GlobalMultiplier.Value = rate;
                SetAllSkillRates(rate);
                _lastGlobal = rate;
            }
            finally
            {
                _applyingPreset = false;
            }
        }

        private static EmploymentPreset MatchPreset(float global)
        {
            if (Approximately(global, 0.75f))
            {
                return EmploymentPreset.GigaUnemployed;
            }

            if (Approximately(global, 1f))
            {
                return EmploymentPreset.Unemployed;
            }

            if (Approximately(global, 1.5f))
            {
                return EmploymentPreset.PartTime;
            }

            if (Approximately(global, 2.5f))
            {
                return EmploymentPreset.FullTime;
            }

            return EmploymentPreset.Custom;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.001f;
        }
    }
}
