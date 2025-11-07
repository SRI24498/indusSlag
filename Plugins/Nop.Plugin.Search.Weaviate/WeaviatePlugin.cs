using Nop.Core.Infrastructure;
using Nop.Plugin.Search.Weaviate.Services;
using Nop.Services.Plugins;
using SearchPioneer.Weaviate.Client;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Nop.Plugin.Search.Weaviate
{
    public class WeaviatePlugin : BasePlugin
    {
        public override async Task InstallAsync()
        {
            var service = EngineContext.Current.Resolve<WeaviateSearchService>();
            await service.IndexAllProductsAsync();
            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            var client = new WeaviateClient(new Config("http", "http://weaviate:8080"));
            await client.Schema.DeleteClassAsync("Product");
            await base.UninstallAsync();
        }
    }
}