// File: /Plugins/Misc.SemanticSearch/Services/HybridSearchService.cs
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.SemanticSearch.Models;
using Nop.Services.Logging;
using Nop.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.SemanticSearch.Services
{
    public class HybridSearchService : IHybridSearchService
    {
        private readonly EmbeddingService _embeddingService;
        private readonly VectorSearchService _vectorService;
        private readonly IRepository<Product> _productRepository;  // ← Use repo directly
        private readonly ILogger _logger;

        public HybridSearchService(
            EmbeddingService embeddingService,
            VectorSearchService vectorService,
            IRepository<Product> productRepository,   // ← NEW
            ILogger logger)
        {
            _embeddingService = embeddingService;
            _vectorService = vectorService;
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task<IList<HybridSearchResultDto>> SearchAsync(string query, int storeId = 0)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<HybridSearchResultDto>();

            var results = new Dictionary<int, (HybridSearchResultDto dto, double score)>();

            try
            {
                // 1. Semantic Search
                var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);
                var semanticResults = await _vectorService.SearchWithScoresAsync(queryVector);
                foreach (var result in semanticResults)
                {
                    var product = await _productRepository.GetByIdAsync(result.ProductId);
                    if (product == null || product.Deleted || !product.Published)
                        continue;

                    var dto = await MapToDto(product);
                    var semanticScore = 1.0 - result.Distance;
                    results[product.Id] = (dto, semanticScore * 0.7);
                }

                // 2. Keyword Search — Use base ProductService via repo + simple query
                var keywordProducts = await _productRepository.Table
                    .Where(p => p.Name.Contains(query) || p.ShortDescription.Contains(query))
                    .Take(20)
                    .ToListAsync();

                foreach (var product in keywordProducts)
                {
                    var dto = await MapToDto(product);
                    double keywordScore = 0.3;
                    if (results.TryGetValue(product.Id, out var existing))
                    {
                        results[product.Id] = (dto, existing.score + keywordScore);
                    }
                    else
                    {
                        results[product.Id] = (dto, keywordScore);
                    }
                }

                // 3. Final ranking
                return results
                    .OrderByDescending(r => r.Value.score)
                    .Take(10)
                    .Select(r => { r.Value.dto.Score = r.Value.score; return r.Value.dto; })
                    .ToList();
            }
            catch (Exception ex)
            {
                await _logger.ErrorAsync("Hybrid search failed", ex);
                return new List<HybridSearchResultDto>();
            }
        }

        private Task<HybridSearchResultDto> MapToDto(Product product)
        {
            return Task.FromResult(new HybridSearchResultDto
            {
                Id = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Price = product.Price
            });
        }
    }
}