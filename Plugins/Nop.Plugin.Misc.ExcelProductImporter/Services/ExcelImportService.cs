// Services/ExcelImportService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.Misc.ExcelProductImporter.Models;
using Nop.Services.Catalog;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.ExcelProductImporter.Services
{
    public class ExcelImportService : IExcelImportService
    {
        private readonly ICategoryService _categoryService;
        private readonly IProductService _productService;
        private readonly IManufacturerService _manufacturerService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IUrlRecordService _urlRecordService;

        public ExcelImportService(
            ICategoryService categoryService,
            IProductService productService,
            IManufacturerService manufacturerService,
            ISpecificationAttributeService specificationAttributeService,
            IProductAttributeService productAttributeService,
            IProductAttributeParser productAttributeParser,
            IUrlRecordService urlRecordService)
        {
            _categoryService = categoryService;
            _productService = productService;
            _manufacturerService = manufacturerService;
            _specificationAttributeService = specificationAttributeService;
            _productAttributeService = productAttributeService;
            _productAttributeParser = productAttributeParser;
            _urlRecordService = urlRecordService;
        }

        public async Task<ImportResultModel> ImportAsync(IFormFile file)
        {
            var result = new ImportResultModel();

            using var reader = new StreamReader(file.OpenReadStream());
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",", // Your CSV uses tas
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var csv = new CsvReader(reader, config);
            var records = csv.GetRecords<CsvRow>().ToList();

            foreach (var row in records)
            {
                // --- Category ---                
                var category = await EnsureCategoryAsync(row.Category);
                result.CategoriesCreated += category.CreatedOnUtc != null ? 1 : 0;

                // --- Manufacturer ---
                var manufacturer = await EnsureManufacturerAsync(row.Manufacturer);
                result.ManufacturersCreated += manufacturer.CreatedOnUtc != null ? 1 : 0;

                // --- Product ---
                var product = new Product
                {
                    ProductType = ProductType.SimpleProduct,
                    VisibleIndividually = true,
                    Name = row.ProductName.Trim(),
                    Sku = row.SKU.Trim(),
                    Price = decimal.Parse(row.Price, CultureInfo.InvariantCulture),
                    OldPrice = decimal.Parse(row.OldPrice, CultureInfo.InvariantCulture),
                    ShortDescription = row.ShortDescription,
                    FullDescription = row.FullDescription,
                    StockQuantity = int.Parse(row.StockQuantity),
                    Weight = decimal.Parse(row.Weight, CultureInfo.InvariantCulture),
                    Published = row.Published.Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                    ManageInventoryMethod = ManageInventoryMethod.ManageStock,
                    CreatedOnUtc = DateTime.UtcNow,
                    UpdatedOnUtc = DateTime.UtcNow,
                    MarkAsNew = true
                };

                await _productService.InsertProductAsync(product);

                // --- Category mapping ---
                await _categoryService.InsertProductCategoryAsync(new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = category.Id,
                    DisplayOrder = 1
                });

                // --- Manufacturer mapping ---
                await _manufacturerService.InsertProductManufacturerAsync(new ProductManufacturer
                {
                    ProductId = product.Id,
                    ManufacturerId = manufacturer.Id,
                    DisplayOrder = 1
                });

                // --- SEO slug ---
                await _urlRecordService.SaveSlugAsync(product, product.Name, 0);
                await _productService.UpdateProductAsync(product);

                // --- Product Attributes (Key=Value;...) ---
                //if (!string.IsNullOrWhiteSpace(row.ProductAttributes))
                //{
                //    //var attrXml = _productAttributeParser.ParseProductAttributeValuesAsync(row.ProductAttributes);
                //    //foreach (var pav in _productAttributeParser.ParseProductAttributeMappingsAsync(attrXml))
                //    //{
                //    //    var pa = await EnsureProductAttributeAsync(pav.ProductAttribute.Name);
                //    //    var pavExisting = await EnsureProductAttributeValueAsync(pa.Id, pav.Name);

                //    //    var mapping = new ProductAttributeMapping
                //    //    {
                //    //        ProductId = product.Id,
                //    //        ProductAttributeId = pa.Id,
                //    //        TextPrompt = pa.Name,
                //    //        IsRequired = false,
                //    //        AttributeControlType = AttributeControlType.TextBox,
                //    //        DisplayOrder = 1
                //    //    };
                //    //    await _productAttributeService.InsertProductAttributeMappingAsync(mapping);

                //    //    var value = new ProductAttributeValue
                //    //    {
                //    //        ProductAttributeMappingId = mapping.Id,
                //    //        Name = pavExisting.Name,
                //    //        IsPreSelected = true
                //    //    };
                //    //    await _productAttributeService.InsertProductAttributeValueAsync(value);
                //    //}
                //    // 1️⃣ Parse all product attribute mappings from the XML
                //    var attributeMappings = await _productAttributeParser.ParseProductAttributeMappingsAsync(row.ProductAttributes);

                //    var attributePairs = row.ProductAttributes
                //                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                //                            .Select(part => part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries))
                //                            .Where(pair => pair.Length == 2)
                //                            .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim(), StringComparer.OrdinalIgnoreCase);


                //    foreach (var kvp in attributePairs)
                //    {
                //        var attributeName = kvp.Key;   // e.g. "Type"
                //        var attributeValue = kvp.Value; // e.g. "Granulated"

                //        // 1️⃣ Ensure the ProductAttribute (e.g., "Type") exists or create it
                //        var productAttribute = await EnsureProductAttributeAsync(attributeName);


                //        // 3️⃣ Create a mapping between the product and the attribute
                //        var productAttributeMapping = new ProductAttributeMapping
                //        {
                //            ProductId = product.Id,
                //            ProductAttributeId = productAttribute.Id,
                //            TextPrompt = productAttribute.Name,
                //            IsRequired = false,
                //            AttributeControlType = AttributeControlType.DropdownList,
                //            DisplayOrder = 1
                //        };
                //        await _productAttributeService.InsertProductAttributeMappingAsync(productAttributeMapping);

                //        // 4️⃣ Parse values for this specific mapping (e.g., Red, Blue)
                //        var attributeValues = await _productAttributeParser.ParseProductAttributeValuesAsync(
                //            row.ProductAttributes
                //        );

                //        foreach (var valueFromXml in attributeValues)
                //        {
                //            // 5️⃣ Ensure the value (e.g., "Red") exists
                //            var ensuredValue = await EnsureProductAttributeValueAsync(productAttribute.Id, valueFromXml.Name);

                //            // 6️⃣ Create and link the ProductAttributeValue to the mapping
                //            var productAttributeValue = new ProductAttributeValue
                //            {
                //                ProductAttributeMappingId = productAttributeMapping.Id,
                //                Name = ensuredValue.Name,
                //                IsPreSelected = true,
                //                DisplayOrder = 1
                //            };
                //            await _productAttributeService.InsertProductAttributeValueAsync(productAttributeValue);
                //        }
                //    }

                //}

                // --- Product Attributes (Key=Value;...) ---
                if (!string.IsNullOrWhiteSpace(row.ProductAttributes))
                {
                    var attributePairs = row.ProductAttributes
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Split('=', 2))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.OrdinalIgnoreCase);

                    foreach (var kvp in attributePairs)
                    {
                        var attributeName = kvp.Key;     // e.g., "Type"
                        var attributeValue = kvp.Value;  // e.g., "Granulated"

                        // 1. Ensure ProductAttribute exists
                        var productAttribute = await EnsureProductAttributeAsync(attributeName);

                        // 2. Create Mapping (Product → Attribute)
                        var mapping = new ProductAttributeMapping
                        {
                            ProductId = product.Id,
                            ProductAttributeId = productAttribute.Id,
                            TextPrompt = productAttribute.Name,
                            IsRequired = false,
                            AttributeControlType = AttributeControlType.DropdownList,
                            DisplayOrder = 1
                        };
                        await _productAttributeService.InsertProductAttributeMappingAsync(mapping);

                        // 3. Ensure Predefined Value exists (optional, but recommended)
                        var predefinedValue = await EnsurePredefinedProductAttributeValueAsync(productAttribute.Id, attributeValue);

                        // 4. Create ProductAttributeValue and link to MAPPING
                        var pav = new ProductAttributeValue
                        {
                            ProductAttributeMappingId = mapping.Id,  // CORRECT ID
                            Name = attributeValue,
                            IsPreSelected = true,
                            DisplayOrder = 1
                        };
                        await _productAttributeService.InsertProductAttributeValueAsync(pav);
                    }
                }

                if (!string.IsNullOrWhiteSpace(row.SpecificationAttributes))
                {
                    // 1️⃣ Parse the key=value pairs into a dictionary
                    var specPairs = ParseKeyValuePairs(row.SpecificationAttributes);

                    // 2️⃣ Iterate over each key/value pair
                    foreach (var (key, value) in specPairs)
                    {
                        // Ensure the Specification Attribute (e.g., "Density") exists or create it
                        var specAttribute = await EnsureSpecificationAttributeAsync(key);

                        // Ensure the Specification Attribute Option (e.g., "3.5 g/cm³") exists or create it
                        var specOption = await EnsureSpecificationAttributeOptionAsync(specAttribute.Id, value);

                        // 3️⃣ Map this option to the product
                        var mapping = new ProductSpecificationAttribute
                        {
                            ProductId = product.Id,
                            SpecificationAttributeOptionId = specOption.Id,
                            AllowFiltering = true,
                            ShowOnProductPage = true,
                            DisplayOrder = 1
                        };

                        await _specificationAttributeService.InsertProductSpecificationAttributeAsync(mapping);
                    }
                }


                result.ProductsCreated++;
            }

            return result;
        }

        private List<(string key, string value)> ParseKeyValuePairs(string input)
        {
            return input.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    var kv = part.Split('=', 2);
                    return (kv[0].Trim(), kv.Length > 1 ? kv[1].Trim() : "");
                })
                .Where(x => !string.IsNullOrEmpty(x.Item1))
                .ToList();
        }

        private async Task<Category> EnsureCategoryAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Uncategorized";
            var cat = await _categoryService.GetAllCategoriesAsync(name);

            if (cat.Count > 0)
                return cat.FirstOrDefault();

            var newCat = new Category
            {
                Name = name,
                Published = true,
                //IncludeInTopMenu = true,
                DisplayOrder = 1,
                CreatedOnUtc = DateTime.UtcNow,
                UpdatedOnUtc = DateTime.UtcNow
            };
            await _categoryService.InsertCategoryAsync(newCat);
            newCat.CreatedOnUtc = DateTime.UtcNow;
            return newCat;
        }

        private async Task<Manufacturer> EnsureManufacturerAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "Unknown";
            var man = await _manufacturerService.GetAllManufacturersAsync(name);
            if (man.Count > 0)
                return man.FirstOrDefault();

            var newMan = new Manufacturer
            {
                Name = name,
                Published = true,
                DisplayOrder = 1,
                CreatedOnUtc = DateTime.UtcNow,
                UpdatedOnUtc = DateTime.UtcNow
            };
            await _manufacturerService.InsertManufacturerAsync(newMan);
            newMan.CreatedOnUtc = DateTime.UtcNow;
            return newMan;
        }

        private async Task<ProductAttribute> EnsureProductAttributeAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Product attribute name cannot be empty.", nameof(name));

            name = name.Trim();

            // Get all attributes (returns IPagedList<ProductAttribute>)
            var allAttributesPaged = await _productAttributeService.GetAllProductAttributesAsync();

            // Convert to a regular list for filtering
            var existing = allAttributesPaged.FirstOrDefault(a =>
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return existing;

            // Create new attribute if not found
            var pa = new ProductAttribute
            {
                Name = name,
                Description = string.Empty
            };

            await _productAttributeService.InsertProductAttributeAsync(pa);
            return pa;
        }


        private async Task<ProductAttributeValue> EnsureProductAttributeValueAsync(int productAttributeId, string name)
        {
            var existing = await _productAttributeService.GetProductAttributeValueByIdAsync(productAttributeId);
            var pav = (existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (pav)
                return existing;

            existing = new ProductAttributeValue
            {
                ProductAttributeMappingId = productAttributeId,
                Name = name
            };
            await _productAttributeService.InsertProductAttributeValueAsync(existing);
            return existing;
        }

        //private async Task<SpecificationAttribute> EnsureSpecificationAttributeAsync(string name)
        //{
        //    var sa = await _specificationAttributeService.GetSpecificationAttributeByNameAsync(name);
        //    if (sa != null)
        //        return sa;

        //    sa = new SpecificationAttribute
        //    {
        //        Name = name,
        //        DisplayOrder = 1
        //    };
        //    await _specificationAttributeService.InsertSpecificationAttributeAsync(sa);
        //    return sa;
        //}

        private async Task<SpecificationAttribute> EnsureSpecificationAttributeAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Specification attribute name cannot be empty.", nameof(name));

            name = name.Trim();

            // nopCommerce doesn’t have GetSpecificationAttributeByNameAsync by default
            var allAttributes = await _specificationAttributeService.GetAllSpecificationAttributesAsync();
            var sa = allAttributes.FirstOrDefault(a =>
                a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (sa != null)
                return sa;

            // Create new attribute if not found
            sa = new SpecificationAttribute
            {
                Name = name,
                DisplayOrder = 1
            };

            await _specificationAttributeService.InsertSpecificationAttributeAsync(sa);
            return sa;
        }


        private async Task<SpecificationAttributeOption> EnsureSpecificationAttributeOptionAsync(int attributeId, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var options = await _specificationAttributeService.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(attributeId);
            var sao = options.FirstOrDefault(o => o.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (sao != null)
                return sao;

            sao = new SpecificationAttributeOption
            {
                SpecificationAttributeId = attributeId,
                Name = value,
                DisplayOrder = 1
            };
            await _specificationAttributeService.InsertSpecificationAttributeOptionAsync(sao);
            return sao;
        }
        private async Task<PredefinedProductAttributeValue> EnsurePredefinedProductAttributeValueAsync(int productAttributeId, string valueName)
{
    var predefined = await _productAttributeService.GetPredefinedProductAttributeValuesAsync(productAttributeId);
    var existing = predefined.FirstOrDefault(p => p.Name.Equals(valueName, StringComparison.OrdinalIgnoreCase));
    if (existing != null)
        return existing;

    var newValue = new PredefinedProductAttributeValue
    {
        ProductAttributeId = productAttributeId,
        Name = valueName,
        DisplayOrder = 1
    };
    await _productAttributeService.InsertPredefinedProductAttributeValueAsync(newValue);
    return newValue;
}
    }

    // CSV Row Model
    public class CsvRow
    {
        public string ProductType { get; set; }
        public string ProductName { get; set; }
        public string SKU { get; set; }
        public string Price { get; set; }
        public string OldPrice { get; set; }
        public string Category { get; set; }
        public string Manufacturer { get; set; }
        public string ShortDescription { get; set; }
        public string FullDescription { get; set; }
        public string StockQuantity { get; set; }
        public string Weight { get; set; }
        public string Published { get; set; }
        public string ProductAttributes { get; set; }
        public string SpecificationAttributes { get; set; }
    }

    // Helper extensions
    public static class EntityExtensions
    {
        public static bool Created { get; set; }
    }
}