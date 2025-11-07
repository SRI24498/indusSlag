// ExcelProductImporterPlugin.cs
using Nop.Services.Common;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.ExcelProductImporter
{
    public class ExcelProductImporterPlugin : BasePlugin, IMiscPlugin
    {        
        public override string GetConfigurationPageUrl() => "/Admin/ExcelImport/Import";

        public override async Task InstallAsync()
        {
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }
    }
}