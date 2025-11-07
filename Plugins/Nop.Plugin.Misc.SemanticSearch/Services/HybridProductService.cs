// File: /Plugins/Misc.SemanticSearch/Services/HybridProductService.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.Misc.SemanticSearch.Models;
using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Shipping.Date;
using Nop.Services.Stores;
using Nop.Services.Vendors;

namespace Nop.Plugin.Misc.SemanticSearch.Services
{
    public class HybridProductService : ProductService
    {
        private readonly IHybridSearchService _hybridSearchService;

        public HybridProductService(
            IHybridSearchService hybridSearchService,
            // ... [30 parameters from ProductService] ...
            CatalogSettings catalogSettings,
            IAclService aclService,
            ICustomerService customerService,
            IDateRangeService dateRangeService,
            ILanguageService languageService,
            ILocalizationService localizationService,
            IProductAttributeParser productAttributeParser,
            IProductAttributeService productAttributeService,
            IRepository<Category> categoryRepository,
            IRepository<CrossSellProduct> crossSellProductRepository,
            IRepository<DiscountProductMapping> discountProductMappingRepository,
            IRepository<LocalizedProperty> localizedPropertyRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<Product> productRepository,
            IRepository<ProductAttributeCombination> productAttributeCombinationRepository,
            IRepository<ProductAttributeMapping> productAttributeMappingRepository,
            IRepository<ProductCategory> productCategoryRepository,
            IRepository<ProductManufacturer> productManufacturerRepository,
            IRepository<ProductPicture> productPictureRepository,
            IRepository<ProductProductTagMapping> productTagMappingRepository,
            IRepository<ProductSpecificationAttribute> productSpecificationAttributeRepository,
            IRepository<ProductTag> productTagRepository,
            IRepository<ProductVideo> productVideoRepository,
            IRepository<ProductWarehouseInventory> productWarehouseInventoryRepository,
            IRepository<RelatedProduct> relatedProductRepository,
            IRepository<Shipment> shipmentRepository,
            IRepository<StockQuantityHistory> stockQuantityHistoryRepository,
            IRepository<TierPrice> tierPriceRepository,
            ISearchPluginManager searchPluginManager,
            IStaticCacheManager staticCacheManager,
            IVendorService vendorService,
            IStoreMappingService storeMappingService,
            IWorkContext workContext,
            LocalizationSettings localizationSettings)
            : base(catalogSettings, aclService, customerService, dateRangeService,
                   languageService, localizationService, productAttributeParser,
                   productAttributeService, categoryRepository, crossSellProductRepository,
                   discountProductMappingRepository, localizedPropertyRepository,
                   manufacturerRepository, productRepository, productAttributeCombinationRepository,
                   productAttributeMappingRepository, productCategoryRepository,
                   productManufacturerRepository, productPictureRepository,
                   productTagMappingRepository, productSpecificationAttributeRepository,
                   productTagRepository, productVideoRepository, productWarehouseInventoryRepository,
                   relatedProductRepository, shipmentRepository, stockQuantityHistoryRepository,
                   tierPriceRepository, searchPluginManager, staticCacheManager,
                   vendorService, storeMappingService, workContext, localizationSettings)
        {
            _hybridSearchService = hybridSearchService;
        }

        public override async Task<IPagedList<Product>> SearchProductsAsync(
            int pageIndex = 0,
            int pageSize = int.MaxValue,
            IList<int> categoryIds = null,
            IList<int> manufacturerIds = null,
            int storeId = 0,
            int vendorId = 0,
            int warehouseId = 0,
            ProductType? productType = null,
            bool visibleIndividuallyOnly = false,
            bool excludeFeaturedProducts = false,
            decimal? priceMin = null,
            decimal? priceMax = null,
            int productTagId = 0,
            string keywords = null,
            bool searchDescriptions = false,
            bool searchManufacturerPartNumber = true,
            bool searchSku = true,
            bool searchProductTags = false,
            int languageId = 0,
            IList<SpecificationAttributeOption> filteredSpecOptions = null,
            ProductSortingEnum orderBy = ProductSortingEnum.Position,
            bool showHidden = false,
            bool? overridePublished = null)
        {
            if (string.IsNullOrWhiteSpace(keywords))
                return await base.SearchProductsAsync(pageIndex, pageSize, categoryIds, manufacturerIds, storeId,
                    vendorId, warehouseId, productType, visibleIndividuallyOnly, excludeFeaturedProducts,
                    priceMin, priceMax, productTagId, keywords, searchDescriptions, searchManufacturerPartNumber,
                    searchSku, searchProductTags, languageId, filteredSpecOptions, orderBy, showHidden, overridePublished);

            // 1. Run hybrid search
            var hybridResults = await _hybridSearchService.SearchAsync(keywords.Trim(), storeId);
            if (!hybridResults?.Any() ?? true)
                return new PagedList<Product>(new List<Product>(), pageIndex, pageSize);

            var rankedProductIds = hybridResults.Select(r => r.Id).ToList();

            // 2. Build query (only filters, no ranking yet)
            var query = _productRepository.Table.Where(p => rankedProductIds.Contains(p.Id));

            // Apply ALL filters
            if (!showHidden)
                query = query.Where(p => p.Published);
            if (overridePublished.HasValue)
                query = query.Where(p => p.Published == overridePublished.Value);
            if (visibleIndividuallyOnly)
                query = query.Where(p => p.VisibleIndividually);
            if (vendorId > 0)
                query = query.Where(p => p.VendorId == vendorId);
            if (productType.HasValue)
                query = query.Where(p => p.ProductTypeId == (int)productType.Value);
            if (priceMin.HasValue)
                query = query.Where(p => p.Price >= priceMin.Value);
            if (priceMax.HasValue)
                query = query.Where(p => p.Price <= priceMax.Value);

            if (categoryIds?.Any() == true)
            {
                var cleanIds = categoryIds.Where(id => id > 0).ToList();
                if (cleanIds.Any())
                    query = query.Where(p => _productCategoryRepository.Table.Any(pc => pc.ProductId == p.Id && cleanIds.Contains(pc.CategoryId)));
            }

            if (manufacturerIds?.Any() == true)
            {
                var cleanIds = manufacturerIds.Where(id => id > 0).ToList();
                if (cleanIds.Any())
                    query = query.Where(p => _productManufacturerRepository.Table.Any(pm => pm.ProductId == p.Id && cleanIds.Contains(pm.ManufacturerId)));
            }

            if (productTagId > 0)
                query = query.Where(p => _productTagMappingRepository.Table.Any(pt => pt.ProductId == p.Id && pt.ProductTagId == productTagId));

            if (filteredSpecOptions?.Any() == true)
            {
                foreach (var spec in filteredSpecOptions)
                    query = query.Where(p => _productSpecificationAttributeRepository.Table.Any(psa => psa.ProductId == p.Id && psa.SpecificationAttributeOptionId == spec.Id));
            }

            // 3. Execute query → get products in DB
            var filteredProducts = await query.ToListAsync();

            // 4. Apply hybrid ranking IN-MEMORY
            var rankedProducts = filteredProducts
                .Select(p => new { Product = p, Rank = rankedProductIds.IndexOf(p.Id) })
                .Where(x => x.Rank >= 0)
                .OrderBy(x => x.Rank)
                .Select(x => x.Product)
                .ToList();

            // 5. Pagination
            var total = rankedProducts.Count;
            var paged = rankedProducts
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedList<Product>(paged, pageIndex, pageSize, total);
        }
    }
}