using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Plugins;

namespace Nop.Plugin.Misc.SemanticSearch
{
    public class SemanticSearchPlugin : BasePlugin
    {
        //private readonly VectorSearchService _vectorService;

        //public SemanticSearchPlugin(VectorSearchService vectorService)
        //{
        //    _vectorService = vectorService;
        //}

        public override async Task InstallAsync()
        {
            //await _vectorService.InitializeCollectionAsync();

            await base.InstallAsync();
        }
        public override async Task UninstallAsync()
        {
            await base.UninstallAsync();
        }
    }
}
