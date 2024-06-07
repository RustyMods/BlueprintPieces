using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BlueprintPieces.Managers;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace BlueprintPieces
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class BlueprintPiecesPlugin : BaseUnityPlugin
    {
        internal const string ModName = "BlueprintPieces";
        internal const string ModVersion = "1.0.2";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource BlueprintPiecesLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public static BlueprintPiecesPlugin _Plugin = null!;
        public static GameObject _Root = null!;
        public static readonly AssetBundle _AssetBundle = GetAssetBundle("blueprintbundle");
        public void Awake()
        {
            _Plugin = this;
            _Root = new GameObject("root");
            _Root.SetActive(false);
            DontDestroyOnLoad(_Root);
            
            Blueprints.ReadFiles();
            Blueprints.SetupServerSync();
            Blueprints.SetupFileWatch();

            InitConfigs();
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void Update()
        {
            if (!Player.m_localPlayer) return;
            if (!Blueprints.SelectedBlueprint()) return;
            if (Input.GetKeyDown(_StepUp.Value)) Blueprints.StepUp();
            if (Input.GetKeyDown(_StepDown.Value)) Blueprints.StepDown();
            if (Input.GetKeyDown(_ResetStep.Value)) Blueprints.ResetStep();
        }

        private void OnDestroy() => Config.Save();

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                BlueprintPiecesLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                BlueprintPiecesLogger.LogError($"There was an issue loading your {ConfigFileName}");
                BlueprintPiecesLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        
        private static AssetBundle GetAssetBundle(string fileName)
        {
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(fileName));
            using Stream? stream = execAssembly.GetManifestResourceStream(resourceName);
            return AssetBundle.LoadFromStream(stream);
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _UseGhostMaterial = null!;
        public static ConfigEntry<Toggle> _SlowBuild = null!;
        public static ConfigEntry<float> _SlowBuildRate = null!;
        public static ConfigEntry<KeyCode> _StepUp = null!;
        public static ConfigEntry<KeyCode> _StepDown = null!;
        public static ConfigEntry<float> _StepIncrement = null!;
        public static ConfigEntry<KeyCode> _ResetStep = null!;
        public static ConfigEntry<Toggle> _EnablePlaceEffects = null!;
        public static ConfigEntry<float> _BuildDelay = null!;

        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            _UseGhostMaterial = config("2 - Settings", "Use Ghost Material", Toggle.Off,
                "If on, placement ghost will use ghost material");

            _SlowBuild = config("2 - Settings", "Slow Build", Toggle.On, "If on, blueprints will build piece by piece");
            _SlowBuildRate = config("2 - Settings", "Build Rate", 0.5f,
                new ConfigDescription("Set the build rate of the slow build feature",
                    new AcceptableValueRange<float>(0.1f, 2f)));

            _StepUp = config("2 - Settings", "Step Up", KeyCode.PageUp,
                "Set the keycode to step up the blueprint in the Y axis for better positioning");
            _StepDown = config("2 - Settings", "Step Down", KeyCode.PageDown,
                "Set the keycode to step down the blueprint in the Y axis for better positioning");
            _StepIncrement = config("2 - Settings", "Step Increment", 0.5f, new ConfigDescription("Set the step increment", new AcceptableValueRange<float>(0.1f, 2f)));
            _ResetStep = config("2 - Settings", "Reset Steps", KeyCode.Escape, "Set the keycode to reset the steps");
            _EnablePlaceEffects = config("2 - Settings", "Place Effects", Toggle.On,
                "If on, a puff of smoke and sound effect appears for each piece built");
            _BuildDelay = config("2 - Settings", "Build Delay", 1f,
                new ConfigDescription("Set the delay for when the build starts building piece-by-piece, for slow build", new AcceptableValueRange<float>(0f, 101f)));
        }

        public ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        public ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        #endregion
    }
}