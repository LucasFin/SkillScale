using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;

namespace SkillScale
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [BepInIncompatibility("Azumatt.AzuSkillTweaks")]
    [BepInIncompatibility("com.odinplusqol.mod")]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "com.ljindustries.valheim.skillscale";
        public const string ModName = "SkillScale";
        public const string ModVersion = "1.0.0";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigSync ConfigSync = new(ModGuid)
        {
            DisplayName = ModName,
            CurrentVersion = ModVersion,
            MinimumRequiredVersion = ModVersion
        };

        private Harmony _harmony;
        private FileSystemWatcher _watcher;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModConfig.Bind(this);

            _harmony = new Harmony(ModGuid);
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Harmony skip {type.FullName}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            SetupWatcher();
            Logger.LogInfo($"{ModName} {ModVersion} loaded.");
        }

        private void OnDestroy()
        {
            _watcher?.Dispose();
            _harmony?.UnpatchSelf();
        }

        internal ConfigEntry<T> BindSynced<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            var extended = description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]");
            var entry = Config.Bind(group, name, value, extended);
            var synced = ConfigSync.AddConfigEntry(entry);
            synced.SynchronizedConfig = synchronizedSetting;
            return entry;
        }

        private void SetupWatcher()
        {
            _watcher = new FileSystemWatcher(Paths.ConfigPath, Path.GetFileName(Config.ConfigFilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName
            };
            _watcher.Changed += ReloadConfig;
            _watcher.Created += ReloadConfig;
            _watcher.Renamed += ReloadConfig;
            _watcher.IncludeSubdirectories = true;
            _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            _watcher.EnableRaisingEvents = true;
        }

        private void ReloadConfig(object sender, FileSystemEventArgs e)
        {
            try
            {
                Config.Reload();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to reload {Config.ConfigFilePath}: {ex.Message}");
            }
        }
    }
}
