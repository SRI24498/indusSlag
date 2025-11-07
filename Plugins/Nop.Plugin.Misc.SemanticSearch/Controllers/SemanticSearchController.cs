using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.SemanticSearch.Models;
using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.SemanticSearch.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public class SemanticSearchController : Controller
    {
        private readonly EmbeddingService _embeddingService;
        private readonly VectorSearchService _vectorService;
        private readonly IProductService _productService;

        public SemanticSearchController(EmbeddingService embeddingService, VectorSearchService vectorService, IProductService productService)
        {
            _embeddingService = embeddingService;
            _vectorService = vectorService;
            _productService = productService;
        }


        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            var results = new Dictionary<int, (HybridSearchResultDto dto, double score)>();

            var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);

            var productIds = await _vectorService.SearchAsync(queryVector);

            foreach (var result in productIds)
            {
                var product = await _productService.GetProductByIdAsync(result.ProductId);
                if (product == null || product.Deleted || !product.Published)
                    continue;

                var dto = await MapToDto(product);
                var semanticScore = 1.0 - result.Distance; // cosine similarity
                results[product.Id] = (dto, semanticScore * 0.7);
            }

            results
               .OrderByDescending(r => r.Value.score)
               .Take(10)
               .Select(r => { r.Value.dto.Score = r.Value.score; return r.Value.dto; })
               .ToList();

            return Ok(new
            {
                query = query,
                count = results.Count,
                results
            });
            //var products = productIds
            //    .Select(id => _productService.GetProductByIdAsync(id))
            //    .Where(p => p != null)
            //    .Select(p => new { p.Result.Name,p.Result.Id,p.Result.FullDescription })
            //    .ToList();

            //return Ok(products);
        }
        private async Task<HybridSearchResultDto> MapToDto(Product product)
        {
            var price = 0; //await _productService.GetProductPriceAsyn(product, 1, includeDiscounts: true);

            return new HybridSearchResultDto
            {
                Id = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Price = price
            };
        }


    }
}
