using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nop.Core.Domain.Catalog;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Catalog;


namespace Nop.Plugin.Misc.ExcelProductImporter.Infrastructure
{
    public class NopStartup : INopStartup
    {
        /// <summary>
        /// Add and configure any of the middleware
        /// </summary>
        /// <param name="services">Collection of service descriptors</param>
        /// <param name="configuration">Configuration of the application</param>
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<EmbeddingService>();
            services.AddScoped<VectorSearchService>();
            // Replace IProductService
            services.RemoveAll<IProductService>();
            services.AddScoped<IProductService, HybridProductService>();

            // Register your services
            services.AddScoped<IHybridSearchService, HybridSearchService>();
        }

        /// <summary>
        /// Configure the using of added middleware
        /// </summary>
        /// <param name="application">Builder for configuring an application's request pipeline</param>
        public void Configure(IApplicationBuilder application)
        {
        }

        /// <summary>
        /// Gets order of this startup configuration implementation
        /// </summary>
        public int Order => 6000;
    }
}
