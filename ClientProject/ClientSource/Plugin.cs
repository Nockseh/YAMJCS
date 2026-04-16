using HarmonyLib;

namespace YAMJCS
{
    public partial class Plugin : IAssemblyPlugin
    {
        // Client-specific code
        public IConfigService ConfigService { get; set; }
        public IPluginManagementService PluginService { get; set; }
        public ILoggerService LoggerService { get; set; }
        //vars
        public Harmony? HarmonyInstance;
        internal static Plugin? PluginInstance { get; private set; }
        
        public void Initialize() {
            PluginInstance = this;
            HarmonyInstance = new Harmony("Nockseh.YAMJCS.Client");
            HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
            //init log
            YAMJ.LoggerService = LoggerService;
            //startup message
            var patched = HarmonyInstance.GetPatchedMethods().ToList();
            YAMJ.Log($"Harmony patched {patched.Count} methods on the client side.");
            foreach (var method in patched)
            {
                YAMJ.Log($"Patched: {method.DeclaringType?.FullName}.{method.Name}");
            }
            YAMJ.Log("Client plugin initialized.");
        }
        public void PreInitPatching() { }
        public void OnLoadCompleted() { }
        public void Dispose() { }
    }
}
