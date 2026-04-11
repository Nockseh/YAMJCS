namespace YAMJCS
{
    public partial class Plugin : IAssemblyPlugin
    {
        // Client-specific code
        public IConfigService ConfigService { get; set; }
        public IPluginManagementService PluginService { get; set; }
        public ILoggerService LoggerService { get; set; }
  
        public void Initialize() {
            YAMJ.LoggerService = LoggerService;
            YAMJ.Log("Client plugin initialized.");
        }
        public void PreInitPatching() { }
        public void OnLoadCompleted() { }
        public void Dispose() { }
    }
}
