using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.SemanticSearch.Services;
using Nop.Services.Catalog;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.SemanticSearch.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public class VectorIndexingController : Controller
    {
        private readonly EmbeddingService _embeddingService;
        private readonly VectorSearchService _vectorService;
        private readonly IProductService _productService;
        private readonly ISpecificationAttributeService _specAttrService;
        private readonly IProductAttributeService _productAttrService;
        private readonly IProductAttributeParser _productAttrParser;

        public VectorIndexingController(
            EmbeddingService embeddingService,
            VectorSearchService vectorService,
            IProductService productService,
            ISpecificationAttributeService specAttrService,
            IProductAttributeService productAttrService,
            IProductAttributeParser productAttrParser)
        {
            _embeddingService = embeddingService;
            _vectorService = vectorService;
            _productService = productService;
            _specAttrService = specAttrService;
            _productAttrService = productAttrService;
            _productAttrParser = productAttrParser;
        }

        [HttpGet]
        public async Task<IActionResult> IndexProducts()
        {
            var products = await _productService.GetProductsMarkedAsNewAsync();

            #region 1. Load ALL Specification Attributes + Options
            var allSpecAttrs = (await _specAttrService.GetAllSpecificationAttributesAsync())
                .ToDictionary(a => a.Id, a => a);

            var allSpecOptions = new Dictionary<int, SpecificationAttributeOption>();
            foreach (var attr in allSpecAttrs.Values)
            {
                var opts = await _specAttrService
                    .GetSpecificationAttributeOptionsBySpecificationAttributeAsync(attr.Id);
                foreach (var opt in opts)
                    allSpecOptions[opt.Id] = opt;
            }
            #endregion

            #region 2. Load ALL Product Attributes + Predefined Values
            var allProductAttrs = (await _productAttrService.GetAllProductAttributesAsync())
                .ToDictionary(a => a.Id, a => a);

            var allPredefinedValues = new Dictionary<int, PredefinedProductAttributeValue>();
            foreach (var attr in allProductAttrs.Values)
            {
                var values = await _productAttrService.GetPredefinedProductAttributeValuesAsync(attr.Id);
                foreach (var v in values)
                    allPredefinedValues[v.Id] = v;
            }
            #endregion

            #region 3. Process each product
            foreach (var product in products)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.Append(product.Name).Append(' ');

                    if (!string.IsNullOrWhiteSpace(product.ShortDescription))
                        sb.Append(product.ShortDescription).Append(' ');

                    if (!string.IsNullOrWhiteSpace(product.FullDescription))
                        sb.Append(product.FullDescription).Append(' ');

                    var specPayload = new List<string>();
                    var attrPayload = new List<string>();

                    #region Specification Attributes
                    var productSpecs = await _specAttrService
                        .GetProductSpecificationAttributesAsync(productId: product.Id);

                    foreach (var mapping in productSpecs)
                    {
                        // Get the option
                        if (!allSpecOptions.TryGetValue(mapping.SpecificationAttributeOptionId, out var opt))
                            continue;

                        // Get the attribute name via option
                        if (!allSpecAttrs.TryGetValue(opt.SpecificationAttributeId, out var attr))
                            continue;

                        string value = !string.IsNullOrWhiteSpace(mapping.CustomValue)
                            ? mapping.CustomValue
                            : opt.Name;

                        sb.Append($"{attr.Name}: {value} ");
                        specPayload.Add($"{attr.Name}: {value}");
                    }
                    #endregion

                    #region Product Attributes – use ConditionAttributeXml + mapping.Id
                    var productAttrMappings = await _productAttrService
                        .GetProductAttributeMappingsByProductIdAsync(product.Id);

                    foreach (var mapping in productAttrMappings)
                    {
                        if (!allProductAttrs.TryGetValue(mapping.ProductAttributeId, out var attr))
                            continue;

                        // Use ConditionAttributeXml (the XML string)
                        var selectedValues = await _productAttrParser
                            .ParseProductAttributeValuesAsync(mapping.ConditionAttributeXml, mapping.Id);

                        var names = selectedValues
                            .Select(pav => pav.Id > 0 &&
                                           allPredefinedValues.TryGetValue(pav.Id, out var pv)
                                           ? pv.Name
                                           : pav.Name)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToArray();

                        if (!names.Any())
                            continue;

                        var finalValue = string.Join(", ", names);
                        sb.Append($"{attr.Name}: {finalValue} ");
                        attrPayload.Add($"{attr.Name}: {finalValue}");
                    }
                    #endregion

                    var textToEmbed = sb.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(textToEmbed))
                        continue;

                    var embedding = await _embeddingService.GenerateEmbeddingAsync(textToEmbed);

                    await _vectorService.InsertProductAsync(
                        productId: product.Id,
                        vector: embedding,
                        name: product.Name,
                        description: $"{product.ShortDescription} {product.FullDescription}".Trim(),
                        specifications: specPayload.ToArray(),
                        attributes: attrPayload.ToArray()
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Index] Product {product.Id}: {ex}");
                }
            }
            #endregion

            return Ok(new { indexed = products.Count });
        }
    }
}