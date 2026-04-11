using HarmonyLib;

namespace YAMJCS

{
    public partial class Plugin : IAssemblyPlugin
    {
        // These are automatically assigned by the plugin service after the Constructor is called
        public IConfigService ConfigService { get; set; }
        public IPluginManagementService PluginService { get; set; }
        public ILoggerService LoggerService { get; set; }
        //vars
        public Harmony Harmony;
        internal static Plugin? Instance { get; private set; }
        
        public void Initialize() {
            Instance = this;
            Harmony = new Harmony("Nockseh.YAMJCS");
            Harmony.PatchAll(typeof(Plugin).Assembly);

            LoggerService.Log($"[YAMJCS] Plugin initialized.");
        }

        public void PreInitPatching() { } //runs after constructor
        public void OnLoadCompleted() { } //for code that relies on other plugins

        public void Dispose() {
            try {
                Harmony?.UnpatchSelf();
            }
            catch (Exception ex) {
                LoggerService?.Log($"[YAMJCS] Failed to unpatch Harmony: {ex}");
            }
            finally {
                Harmony = null;
                Instance = null;
            }
        }
    }
}
