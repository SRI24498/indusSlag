// src\Plugins\Nop.Plugin.Search.Weaviate\Services\WeaviateSearchService.cs
using Microsoft.Extensions.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using System.Net.Http;
using System.Text.Json;
using WeaviateNET;
using WeaviateNET.Query;

namespace Nop.Plugin.Search.Weaviate.Services
{
    public class WeaviateSearchService
    {
        private readonly WeaviateClient _client;
        private readonly IProductService _productService;
        private const string ClassName = "Product";

        public WeaviateSearchService(
            IConfiguration config,
            IProductService productService,
            IHttpClientFactory httpFactory)
        {
            var url = config["Weaviate:Url"] ?? "http://weaviate:8080";

            // <-- IMPORTANT: use the constructor you showed -->
            var httpClient = httpFactory.CreateClient();
            httpClient.BaseAddress = new Uri(url);

            _client = new WeaviateClient(url, httpClient);
            _productService = productService;
        }

        #region Schema
        public async Task EnsureSchemaAsync()
        {
            // Check if class already exists
            var schema = await _client.Schema.GetAsync();
            if (schema.Classes.Any(c => c.Class == ClassName))
                return;

            var classObj = new WeaviateClass
            {
                Class = ClassName,
                Vectorizer = "text2vec-openai",
                ModuleConfig = new Dictionary<string, object>
                {
                    ["text2vec-openai"] = new { model = "text-embedding-3-small" }
                },
                Properties = new[]
                {
                    new Property { Name = "name",        DataType = new[] { "text" } },
                    new Property { Name = "description", DataType = new[] { "text" } },
                    new Property { Name = "sku",         DataType = new[] { "text" } },
                    new Property { Name = "price",       DataType = new[] { "number" } }
                }
            };

            await _client.Schema.CreateClassAsync(classObj);
        }
        #endregion

        #region Indexing
        public async Task IndexAllProductsAsync()
        {
            await EnsureSchemaAsync();

            var products = await _productService.SearchProductsAsync(pageIndex: 0, pageSize: 10000);
            var objects = products.Select(p => new
            {
                @class = ClassName,
                properties = new
                {
                    name = p.Name,
                    description = p.ShortDescription ?? p.FullDescription ?? "",
                    sku = p.Sku ?? "",
                    price = p.Price
                }
            }).ToList();

            if (!objects.Any())
                return;

            // Batch-create (100 objects per request – safe default)
            const int batchSize = 100;
            for (int i = 0; i < objects.Count; i += batchSize)
            {
                var batch = objects.Skip(i).Take(batchSize);
                await _client.Data.BatchCreateObjectsAsync(batch);
            }
        }
        #endregion

        #region Search
        public async Task<List<Product>> SearchAsync(string query, decimal? maxPrice = null, int limit = 10)
        {
            var builder = _client.GraphQL
                .Get()
                .WithClassName(ClassName)
                .WithNearText(new { concepts = new[] { query } })
                .WithLimit(limit)
                .WithFields("name sku price description _additional { distance }");

            if (maxPrice.HasValue)
            {
                builder.WithWhere(new
                {
                    path = new[] { "price" },
                    @operator = "LessThanEqual",
                    valueNumber = (double)maxPrice.Value
                });
            }

            var result = await builder.DoAsync();

            var mapped = new List<Product>();

            foreach (var item in result.Data.Get.Product)
            {
                var name = item.Name ?? "";
                var sku = item.Sku ?? "";
                var price = item.Price;

                // Prefer real product from DB
                var product = string.IsNullOrWhiteSpace(sku)
                    ? null
                    : await _productService.GetProductBySkuAsync(sku);

                if (product == null)
                {
                    product = new Product
                    {
                        Name = name,
                        Sku = sku,
                        Price = price,
                        ShortDescription = item.Description
                    };
                }

                mapped.Add(product);
            }

            return mapped;
        }
        #endregion
    }
}