using HarmonyLib;

namespace YAMJCS
{
    public partial class Plugin : IAssemblyPlugin
    {
        // Server-specific code
        public IConfigService ConfigService { get; set; }
        public IPluginManagementService PluginService { get; set; }
        public ILoggerService LoggerService { get; set; }
        //vars
        public Harmony? HarmonyInstance;
        internal static Plugin? PluginInstance { get; private set; }
        
        public void Initialize() {
            PluginInstance = this;
            HarmonyInstance = new Harmony("Nockseh.YAMJCS.Server");
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);

            var patched = HarmonyInstance.GetPatchedMethods().ToList();
            LoggerService.Log($"[YAMJCS] Harmony patched {patched.Count} methods.");
            foreach (var method in patched)
            {
                LoggerService.Log($"[YAMJCS] Patched: {method.DeclaringType?.FullName}.{method.Name}");
            }
            LoggerService.Log($"[YAMJCS] Server plugin initialized.");
        }

        public void PreInitPatching() { } //runs after constructor
        public void OnLoadCompleted() { } //for code that relies on other plugins

        public void Dispose() {
            try {
                HarmonyInstance?.UnpatchSelf();
            }
            catch (Exception ex) {
                LoggerService?.Log($"[YAMJCS] Failed to unpatch Harmony: {ex}");
            }
            finally {
                HarmonyInstance = null;
                PluginInstance = null;
            }
        }
    }
}
