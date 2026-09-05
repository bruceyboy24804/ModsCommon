namespace ModsCommon.Mod {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Colossal;
    using Colossal.IO.AssetDatabase;
    using Colossal.Json;
    using Colossal.Localization;
    using Colossal.Logging;
    using Colossal.Reflection;
    using Colossal.TestFramework;
    using Colossal.UI;

    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    using HarmonyLib;

    using ModsCommon.Extensions;
    using ModsCommon.Utils;

    using Newtonsoft.Json;

    using UnityEngine;

    using StreamReader = System.IO.StreamReader;

    #endregion

    /// <summary>
    /// Abstract base class for CS2 mods. Provides common lifecycle management including
    /// logging, settings, localization, Harmony patching, asset registration, and test registration.
    /// </summary>
    /// <typeparam name="TSelf">The derived mod type, enabling typed static Instance access.</typeparam>
    // NOTE: ModsCommonBase intentionally does NOT implement IMod. The game instantiates every
    // IMod-derived type in the assembly (ModManager: GetTypesDerivedFrom<IMod>() ->
    // FormatterServices.GetUninitializedObject), and because shared code is compiled into the mod's
    // own assembly (source inclusion), an abstract IMod base here would be picked up and crash with
    // "Type cannot be instantiated". The concrete mod class declares `: ModsCommonBase<TSelf>, IMod`
    // and satisfies IMod via the inherited public OnLoad/OnDispose below.
    public abstract class ModsCommonBase<TSelf> where TSelf : ModsCommonBase<TSelf>, IMod {
        private Harmony        m_Harmony;
        private PrefixedLogger m_Log;

        /// <summary>
        /// Gets the singleton instance of the mod.
        /// </summary>
        public static TSelf Instance { get; private set; }

        /// <summary>
        /// Gets the mod's raw logger.
        /// </summary>
        public ILog Log { get; private set; }

        /// <summary>
        /// Gets the mod's prefixed logger.
        /// </summary>
        public PrefixedLogger ModLog => m_Log;

        /// <summary>
        /// Gets the mod's settings. Use <c>new</c> keyword in derived class to shadow with typed property.
        /// </summary>
        public ModSetting Settings { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether the mod is in test mode.
        /// </summary>
        protected internal bool IsTestMode { get; set; } = false;

        #region Abstract Members

        /// <summary>
        /// Gets the mod's display name.
        /// </summary>
        public abstract string ModName { get; }

        /// <summary>
        /// Gets the mod's binding Id used for UI bindings between C# and TypeScript.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the UI host location prefix used for AddHostLocation.
        /// </summary>
        protected abstract string UiHostPrefix { get; }

        /// <summary>
        /// Creates the mod-specific settings instance.
        /// </summary>
        /// <param name="mod">The mod instance.</param>
        /// <returns>A new settings instance.</returns>
        protected abstract ModSetting CreateSettings(IMod mod);

        /// <summary>
        /// Creates the en-US localization dictionary source.
        /// </summary>
        /// <param name="settings">The mod's settings.</param>
        /// <returns>The en-US dictionary source.</returns>
        protected abstract IDictionarySource CreateEnUsLocalization(ModSetting settings);

        /// <summary>
        /// Registers all mod-specific ECS systems with the update system.
        /// </summary>
        /// <param name="updateSystem">The update system to register into.</param>
        protected abstract void RegisterSystems(UpdateSystem updateSystem);

        #endregion

        #region Virtual Hooks

        /// <summary>
        /// Called after core initialization (logger, settings, localization) but before
        /// Harmony patching and system registration. Override for mod-specific post-init logic.
        /// </summary>
        /// <param name="updateSystem">The update system.</param>
        protected virtual void OnAfterLoad(UpdateSystem updateSystem) { }

        /// <summary>
        /// Called at the start of OnDispose, before settings unregistration and Harmony teardown.
        /// Override for mod-specific cleanup.
        /// </summary>
        protected virtual void OnBeforeDispose() { }

        /// <summary>
        /// Override to generate a language file in debug/I18N builds. Call
        /// <see cref="ExportEnUsLocalization"/> to do the heavy lifting — the override just provides the
        /// <c>[CallerFilePath]</c> anchor so the export lands next to the mod's source.
        /// </summary>
        protected virtual void GenerateLanguageFile() { }

        /// <summary>
        /// Serializes the en-US localization dictionary to a JSON file relative to the calling source
        /// file. Intended to be called from a <see cref="GenerateLanguageFile"/> override like:
        /// <code>protected override void GenerateLanguageFile() => ExportEnUsLocalization();</code>
        /// </summary>
        /// <param name="relativePath">Path relative to the caller's directory (default: L10n/lang/en-US.json).</param>
        /// <param name="callerFilePath">Auto-populated by the compiler.</param>
        protected void ExportEnUsLocalization(string relativePath = "L10n/lang/en-US.json", [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null) {
            m_Log.Debug("ExportEnUsLocalization()");
            var localeDict = CreateEnUsLocalization(Settings)
                .ReadEntries(new List<IDictionaryEntryError>(), new Dictionary<string, int>())
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var json = JsonConvert.SerializeObject(localeDict, Formatting.Indented);
            try {
                var directory  = Path.GetDirectoryName(callerFilePath);
                var exportPath = Path.Combine(directory, relativePath);
                File.WriteAllText(exportPath, json);
                m_Log.Info($"Exported language file to {exportPath}");
            } catch (Exception ex) {
                m_Log.Error($"Failed to export language file: {ex}");
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the mod's informational version from assembly metadata.
        /// </summary>
        public string InformationalVersion =>
            GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        /// <summary>
        /// Gets the mod's version string.
        /// </summary>
        public string Version => GetType().Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

        private string HarmonyPatchId => $"{ModName}.{GetType().Name}";

        #endregion

        /// <inheritdoc/>
        public void OnLoad(UpdateSystem updateSystem) {
            // Set instance reference.
            Instance = (TSelf)this;

            // Initialize logger.
            Log = LogManager
                  .GetLogger(ModName)
                  .SetShowsErrorsInUI(false);
#if IS_DEBUG
            Log = Log
                  .SetBacktraceEnabled(true)
                  .SetEffectiveness(Level.All);
#endif
            m_Log = new PrefixedLogger(GetType().Name, Log);
            m_Log.Info($"Loading {ModName} version {GetType().Assembly.GetName().Version}");

            // Initialize shared utilities.
            PrefixedLogger.DefaultLog = Log;
            ReflectionExtensions.Initialize(Log);
            LocaleHelper.Initialize(ModName);

            // Initialize Settings.
            Settings = CreateSettings(Instance);

            // Load i18n.
            GameManager.instance.localizationManager.AddSource("en-US", CreateEnUsLocalization(Settings));
            LoadNonEnglishLocalizations();

            // Generate i18n files in debug builds.
#if IS_DEBUG && EXPORT_EN_US
            GenerateLanguageFile();
#endif

            // Register mod settings to game options UI.
            Settings.RegisterInOptionsUI();

            // Load saved settings.
            AssetDatabase.global.LoadSettings(ModName, Settings, CreateSettings(Instance));

            // Apply input bindings.
            Settings.RegisterKeyBindings();

            // Mod-specific post-init hook.
            OnAfterLoad(updateSystem);

            // Harmony patches.
            InitializeHarmonyPatches();

            // Register systems.
            RegisterSystems(updateSystem);

            // Add tests.
#if IS_DEBUG
            AddTests();
#endif

            // Register UI assets.
            RegisterAssets();

            m_Log.Info($"Installed and enabled. RenderedFrame: {Time.renderedFrameCount}");
        }

        /// <inheritdoc/>
        public void OnDispose() {
            // Guard: OnLoad may have failed before the logger was initialized.
            if (m_Log != null) {
                m_Log.Info("OnDispose()");
            }

            OnBeforeDispose();

            Instance = null;

            // Clear settings menu entry.
            if (Settings != null) {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }

            // Unpatch Harmony.
            TeardownHarmonyPatches();
        }

        private void InitializeHarmonyPatches() {
            m_Log.Debug("InitializeHarmonyPatches()");

            m_Harmony = new Harmony(HarmonyPatchId);
            m_Harmony.PatchAll(GetType().Assembly);
            var patchedMethods = m_Harmony.GetPatchedMethods().ToArray();

            foreach (var patchedMethod in patchedMethods) {
                m_Log.Debug($"InitializeHarmonyPatches() -- Patched method: {patchedMethod.Module.ScopeName}:{patchedMethod.Name}");
            }
        }

        private void TeardownHarmonyPatches() {
            m_Log?.Debug("TeardownHarmonyPatches()");
            m_Harmony?.UnpatchAll(HarmonyPatchId);
        }

        private void RegisterAssets() {
            m_Log.Debug("RegisterAssets()");
            if (!GameManager.instance.modManager.TryGetExecutableAsset(Instance, out var modAsset)) {
                m_Log.Error("Failed to get executable asset path. Exiting.");
                return;
            }

            var assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
            var assetsPath   = assemblyPath + "/Assets/";

            // AddHostLocation starts a file watcher on the directory, so pointing it at one that does
            // not exist throws an ArgumentException the game logs as a red error with a full stack
            // trace on every launch. A mod that ships no assets is a normal state, not a fault.
            if (!Directory.Exists(assetsPath)) {
                m_Log.Debug($"RegisterAssets() -- No assets directory at {assetsPath}, nothing to host.");
                return;
            }

            m_Log.Debug($"RegisterAssets() -- Registering assets from {assetsPath} to {UiHostPrefix}");
            UIManager.defaultUISystem.AddHostLocation(UiHostPrefix, assetsPath);
        }

        private void AddTests() {
            m_Log.Debug("AddTests()");

            var field = typeof(TestScenarioSystem).GetField("m_Scenarios", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                m_Log.Error("AddTests() -- Could not find m_Scenarios");
                return;
            }

            var scenarios = (Dictionary<string, TestScenarioSystem.Scenario>)field.GetValue(TestScenarioSystem.instance);

            foreach (var type in GetTests()) {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !type.TryGetAttribute(
                        out TestDescriptorAttribute testDescriptorAttribute)) {
                    continue;
                }

                m_Log.Debug($"AddTests() -- {testDescriptorAttribute.description}");

                scenarios.Add(
                    testDescriptorAttribute.description,
                    new TestScenarioSystem.Scenario {
                        category  = testDescriptorAttribute.category,
                        testPhase = testDescriptorAttribute.testPhase,
                        test      = type,
                        disabled  = testDescriptorAttribute.disabled,
                    });
            }

            scenarios = TestScenarioSystem.SortScenarios(scenarios);

            field.SetValue(TestScenarioSystem.instance, scenarios);
        }

        private IEnumerable<Type> GetTests() {
            return from t in GetType().Assembly.GetTypes()
                where typeof(TestScenario).IsAssignableFrom(t)
                select t;
        }

        private void LoadNonEnglishLocalizations() {
            var modAssembly   = GetType().Assembly;
            var resourceNames = modAssembly.GetManifestResourceNames();

            try {
                m_Log.Debug("Reading localizations");

                foreach (var localeID in GameManager.instance.localizationManager.GetSupportedLocales()) {
                    string resourceName = null;
                    foreach (var candidate in new[] {
                        $"{modAssembly.GetName().Name}.L10n.lang.{localeID}.json",
                        $"{modAssembly.GetName().Name}.lang.{localeID}.json",
                    }) {
                        if (resourceNames.Contains(candidate)) {
                            resourceName = candidate;
                            break;
                        }
                    }

                    if (resourceName != null) {
                        m_Log.Debug($"Found localization file {resourceName}");
                        try {
                            m_Log.Debug($"Reading embedded translation file {resourceName}");

                            using StreamReader reader = new(modAssembly.GetManifestResourceStream(resourceName));
                            {
                                var entireFile   = reader.ReadToEnd();
                                var varient      = JSON.Load(entireFile);
                                var translations = varient.Make<Dictionary<string, string>>();
                                GameManager.instance.localizationManager.AddSource(localeID, new MemorySource(translations));
                            }
                        } catch (Exception e) {
                            m_Log.Error($"Exception reading localization from embedded file {resourceName}: {e}");
                        }
                    } else {
                        m_Log.Debug($"Did not find localization file for {localeID}");
                    }
                }
            } catch (Exception e) {
                m_Log.Error($"Exception reading embedded settings localization files: {e}");
            }
        }
    }
}
