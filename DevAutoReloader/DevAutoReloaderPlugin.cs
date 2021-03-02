using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenMod.API;
using OpenMod.API.Plugins;
using OpenMod.Core.Helpers;
using OpenMod.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

[assembly: PluginMetadata("DevAutoReloader", DisplayName = "Dev Auto Reloader", Author = "Stephen White")]

namespace DevAutoReloader
{
    public class DevAutoReloaderPlugin : OpenModUniversalPlugin
    {
        private readonly IRuntime _runtime;

        private readonly string _watchedPath;
        private FileSystemWatcher? _watcher;

        private bool _reloading;

        public DevAutoReloaderPlugin(IRuntime runtime,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _runtime = runtime;
            _watchedPath = Path.Combine(_runtime.WorkingDirectory, "plugins");

            _reloading = false;
        }

        protected override Task OnLoadAsync()
        {
            Logger.LogInformation("Watching path for plugin updates: " + _watchedPath);

            _watcher = new FileSystemWatcher(_watchedPath, "*.dll")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };
            
            _watcher.Changed += OnPluginsUpdated;

            return Task.CompletedTask;
        }

        private void OnPluginsUpdated(object sender, FileSystemEventArgs args)
        {
            try
            {
                if (_reloading) return;

                var fileName = Path.GetFileName(args.FullPath);

                Logger.LogDebug($"File change - '{fileName}' ({args.ChangeType})");

                if (args.ChangeType == WatcherChangeTypes.Renamed ||
                    args.ChangeType == WatcherChangeTypes.Deleted)
                {
                    return;
                }

                var ignored = Configuration.GetValue<string[]?>("ignoredPlugins");

                if (ignored != null && ignored.Contains(fileName))
                {
                    Logger.LogDebug($"Detected ignored plugin file change - '{fileName}'");
                    return;
                }

                _reloading = true;
                Logger.LogInformation($"Detected plugin file change - '{fileName}'. Reloading OpenMod...");

                AsyncHelper.Schedule("DevAutoReloader-ReloadOpenMod", () => _runtime.PerformSoftReloadAsync());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error occurred on file change for: {args.FullPath}");
            }
        }

        protected override Task OnUnloadAsync()
        {
            _watcher?.Dispose();

            return Task.CompletedTask;
        }
    }
}
