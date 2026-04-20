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
            //init log
            YAMJ.LoggerService = LoggerService;
            //startup message
            var patched = HarmonyInstance.GetPatchedMethods().ToList();
            YAMJ.Log($"Harmony patched {patched.Count} methods on the server side.");
            foreach (var method in patched)
            {
                YAMJ.Log($"Patched: {method.DeclaringType?.FullName}.{method.Name}");
            }
            YAMJ.Log("Server plugin initialized.");
        }

        public void PreInitPatching() { } //runs after constructor

        public void OnLoadCompleted() { //for code that relies on other plugins
            foreach (var prefab in AfflictionPrefab.Prefabs) {
                if (prefab.Identifier == "yamjHunger".ToIdentifier()) YAMJ.HungerPrefab = prefab;
            }
        }

        public void Dispose() {
            try {
                HarmonyInstance?.UnpatchSelf();
            }
            catch (Exception ex) {
                YAMJ.Log($"Failed to unpatch Harmony: {ex}");
            }
            finally {
                HarmonyInstance = null;
                PluginInstance = null;
            }
        }
    }
}
