using fabrics.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Spreadsheet;
using fabrics.Models;
using fabrics.Services.Interface;
using AirtableApiClient;

namespace fabrics.Services
{
    public class AirtableService : IAirtableService
    {
        private readonly string _apiKey;
        private readonly string _baseId;
        private readonly TelegramService _telegram;
        private readonly AirtableBase _airtableBase;
        private readonly string _categoriesTableName;
        private readonly string _productsTableName;

        public AirtableService(IConfiguration config, TelegramService telegram)
        {
            _telegram = telegram;
            _apiKey = config["Airtable:ApiKey"];
            _baseId = config["Airtable:BaseId"];
            _categoriesTableName = "Categories";
            _productsTableName = "Products";
            _airtableBase = new AirtableBase(_apiKey, _baseId);
        }

        // ✅ دوال التسجيل المحسنة
        private void LogInfo(string message) => Console.WriteLine($"ℹ️ {DateTime.Now:HH:mm:ss} - {message}");
        private void LogError(string message, Exception ex = null) =>
            Console.WriteLine($"❌ {DateTime.Now:HH:mm:ss} - {message} {ex?.Message}");

        // ✅ دالة مساعدة محسنة لاستخراج IDs من الحقول المرتبطة
        private string[] ExtractLinkedRecordIds(object fieldValue)
        {
            if (fieldValue == null) return new string[0];

            try
            {
                if (fieldValue is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        var ids = jsonElement.EnumerateArray()
                            .Select(e =>
                            {
                                try
                                {
                                    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("id", out var idProperty))
                                    {
                                        return idProperty.GetString();
                                    }
                                    return e.GetString();
                                }
                                catch
                                {
                                    return null;
                                }
                            })
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToArray();

                        LogInfo($"Extracted {ids.Length} IDs from array");
                        return ids;
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        var singleId = jsonElement.GetString();
                        var result = string.IsNullOrEmpty(singleId) ? new string[0] : new[] { singleId };
                        LogInfo($"Extracted single ID: {singleId}");
                        return result;
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        if (jsonElement.TryGetProperty("id", out var idProperty))
                        {
                            var id = idProperty.GetString();
                            var result = string.IsNullOrEmpty(id) ? new string[0] : new[] { id };
                            LogInfo($"Extracted ID from object: {id}");
                            return result;
                        }
                    }
                }
                else if (fieldValue is List<object> objectList)
                {
                    var ids = objectList
                        .Select(obj => obj?.ToString())
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToArray();
                    LogInfo($"Extracted {ids.Length} IDs from object list");
                    return ids;
                }
                else if (fieldValue is string singleValue && !string.IsNullOrEmpty(singleValue))
                {
                    LogInfo($"Extracted single string ID: {singleValue}");
                    return new[] { singleValue };
                }
            }
            catch (Exception ex)
            {
                LogError($"Error parsing linked field - Type: {fieldValue?.GetType().Name}", ex);
            }

            LogInfo("No IDs extracted from field");
            return new string[0];
        }

        // ✅ دالة مساعدة محسنة لتحليل سجل الفئة
        private async Task<Category> ParseCategoryRecord(AirtableRecord record)
        {
            var category = new Category
            {
                Id = record.Id,
                Name = record.GetField<string>("Name") ?? "غير معروف"
            };

            // معالجة ParentCategory مع الهيكل الجديد
            var parentCategoryValue = record.GetField<object>("ParentCategory");
            category.ParentCategory = ExtractLinkedRecordIds(parentCategoryValue);

            // جلب عدد المنتجات إذا كان الحقل موجوداً
            try
            {
                var productsCount = record.GetField<long?>("Products Count");
                category.ProductsCount = (int)(productsCount ?? 0);
            }
            catch
            {
                category.ProductsCount = 0;
            }

            return category;
        }

        // ✅ جلب كل التصنيفات
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            var categories = new List<Category>();

            try
            {
                var response = await _airtableBase.ListRecords(_categoriesTableName);

                if (response.Success)
                {
                    LogInfo("=== FETCHING ALL CATEGORIES ===");

                    foreach (var record in response.Records)
                    {
                        var category = await ParseCategoryRecord(record);
                        categories.Add(category);

                        LogInfo($"Category: {category.Name}, ID: {category.Id}, " +
                                $"Parent: {(category.ParentCategory?.FirstOrDefault() ?? "NULL")}, " +
                                $"IsMain: {category.IsMainCategory}, ProductsCount: {category.ProductsCount}");
                    }

                    LogInfo($"✅ Successfully fetched {categories.Count} categories");
                }
                else
                {
                    LogError($"Airtable Error: {response.AirtableApiError?.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                LogError("Error fetching categories", ex);
            }

            return categories;
        }

        // ✅ جلب التصنيفات الرئيسية فقط
        public async Task<List<Category>> GetMainCategoriesAsync()
        {
            try
            {
                // استخدام فلتر مباشر في Airtable
                var formula = "ISBLANK({ParentCategory})";
                var response = await _airtableBase.ListRecords(_categoriesTableName, filterByFormula: formula);

                var mainCategories = new List<Category>();

                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        var category = await ParseCategoryRecord(record);
                        mainCategories.Add(category);
                    }

                    LogInfo($"✅ Main Categories Found (Filtered): {mainCategories.Count}");
                }
                else
                {
                    LogError($"Airtable Error in GetMainCategoriesAsync: {response.AirtableApiError?.ErrorMessage}");
                }

                return mainCategories;
            }
            catch (Exception ex)
            {
                LogError("Error in GetMainCategoriesAsync", ex);
                // Fallback
                var allCategories = await GetAllCategoriesAsync();
                var fallbackMainCategories = allCategories.Where(c => c.IsMainCategory).ToList();
                LogInfo($"🔄 Using Fallback - Found: {fallbackMainCategories.Count} main categories");
                return fallbackMainCategories;
            }
        }

        // ✅ جلب التصنيفات الفرعية لتصنيف معين
        public async Task<List<Category>> GetSubCategoriesAsync(string parentCategoryId)
        {
            try
            {
                // الطريقة المثلى: استخدام Filter مباشرة في Airtable
                var formula = $"{{ParentCategory}} = '{parentCategoryId}'";
                var response = await _airtableBase.ListRecords(_categoriesTableName, filterByFormula: formula);

                var subCategories = new List<Category>();

                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        var category = await ParseCategoryRecord(record);
                        subCategories.Add(category);
                        LogInfo($"Found SubCategory: {category.Name} for Parent: {parentCategoryId}");
                    }
                }

                LogInfo($"✅ Total SubCategories for {parentCategoryId}: {subCategories.Count}");
                return subCategories;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetSubCategoriesAsync for {parentCategoryId}", ex);

                // Fallback: الطريقة الاحتياطية
                try
                {
                    var allCategories = await GetAllCategoriesAsync();
                    var fallbackSubCategories = allCategories.Where(c =>
                        c.ParentCategory != null &&
                        c.ParentCategory.Contains(parentCategoryId)
                    ).ToList();

                    LogInfo($"🔄 Using Fallback - Found: {fallbackSubCategories.Count} subcategories");
                    return fallbackSubCategories;
                }
                catch (Exception fallbackEx)
                {
                    LogError("Fallback also failed", fallbackEx);
                    return new List<Category>();
                }
            }
        }

        // ✅ دالة مساعدة لملء MainCategory المفقودة
        private async Task FillMissingMainCategories(Product product)
        {
            try
            {
                var mainCategories = new List<string>();
                foreach (var subCategoryId in product.SubCategory)
                {
                    var mainCategory = await GetMainCategoryAsync(subCategoryId);
                    if (mainCategory != null)
                    {
                        mainCategories.Add(mainCategory.Id);
                    }
                }
                product.MainCategory = mainCategories.Distinct().ToArray();

                if (mainCategories.Any())
                {
                    LogInfo($"Filled missing main categories for product {product.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error filling main categories for product {product.Name}", ex);
            }
        }

        // ✅ دالة مساعدة لتحليل سجل المنتج (معدلة)
        private async Task<Product> ParseProductRecord(AirtableRecord record)
        {
            var product = new Product
            {
                Id = record.Id,
                Name = record.GetField<string>("Name") ?? "غير معروف",
                Description = record.GetField<string>("Description") ?? "",
                PricePerMeter = record.GetField<decimal?>("PricePerMeter") ?? 0,
                Image = record.GetField<string>("Image") ?? ""
            };

            try
            {
                // معالجة الحقول الجديدة مع تحسين الأخطاء
                product.SubCategory = ExtractLinkedRecordIds(record.GetField<object>("SubCategory"));
                product.MainCategory = ExtractLinkedRecordIds(record.GetField<object>("MainCategory"));

                // إذا كانت MainCategory فارغة ولكن SubCategory موجودة، املأها
                if (product.MainCategory.Length == 0 && product.SubCategory.Length > 0)
                {
                    await FillMissingMainCategories(product);
                }

                // للحفاظ على التوافق
                product.Category = product.SubCategory.Concat(product.MainCategory).Distinct().ToArray();
            }
            catch (Exception ex)
            {
                LogError($"Error parsing product {product.Name}", ex);
            }

            return product;
        }

        // ✅ جلب المنتجات حسب الفئة (معدل للهيكل الجديد)
        public async Task<List<Product>> GetProductsByCategoryAsync(string categoryId)
        {
            var products = new List<Product>();

            try
            {
                // البحث في كلا الحقلين
                var formula = $"{{SubCategory}} = '{categoryId}' OR {{MainCategory}} = '{categoryId}'";
                var response = await _airtableBase.ListRecords(_productsTableName, filterByFormula: formula);

                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        var product = await ParseProductRecord(record);
                        products.Add(product);
                    }
                }

                LogInfo($"Found {products.Count} products for category {categoryId}");
            }
            catch (Exception ex)
            {
                LogError($"Error fetching products for category {categoryId}", ex);
            }

            return products;
        }

        // ✅ جلب كل منتجات الفئة الرئيسية (بما فيها الفرعية)
        public async Task<List<Product>> GetProductsByMainCategoryAsync(string mainCategoryId)
        {
            var allProducts = new List<Product>();

            try
            {
                LogInfo($"Getting products for main category: {mainCategoryId}");

                // 1. جلب الفئات الفرعية
                var subCategories = await GetSubCategoriesAsync(mainCategoryId);
                LogInfo($"Found {subCategories.Count} subcategories");

                // 2. جلب منتجات كل فئة فرعية
                foreach (var subCategory in subCategories)
                {
                    var subProducts = await GetProductsByCategoryAsync(subCategory.Id);
                    allProducts.AddRange(subProducts);
                    LogInfo($"Found {subProducts.Count} products in subcategory: {subCategory.Name}");
                }

                // 3. جلب المنتجات المرتبطة مباشرة بالفئة الرئيسية
                var directProducts = await GetProductsByCategoryAsync(mainCategoryId);
                if (directProducts.Any())
                {
                    LogInfo($"Found {directProducts.Count} products directly in main category");
                    allProducts.AddRange(directProducts);
                }

                // إزالة التكرارات
                var distinctProducts = allProducts.DistinctBy(p => p.Id).ToList();
                LogInfo($"Total distinct products: {distinctProducts.Count}");

                return distinctProducts;
            }
            catch (Exception ex)
            {
                LogError($"Error in GetProductsByMainCategoryAsync for {mainCategoryId}", ex);
                return new List<Product>();
            }
        }

        // ✅ جلب الفئة الرئيسية من أي فئة
        public async Task<Category> GetMainCategoryAsync(string categoryId)
        {
            try
            {
                var allCategories = await GetAllCategoriesAsync();
                var category = allCategories.FirstOrDefault(c => c.Id == categoryId);

                if (category?.ParentCategory?.FirstOrDefault() != null)
                {
                    // إذا كانت فئة فرعية، أرجع الفئة الرئيسية التابعة لها
                    var mainCategoryId = category.ParentCategory[0];
                    var mainCategory = allCategories.FirstOrDefault(c => c.Id == mainCategoryId);
                    LogInfo($"Found main category {mainCategory?.Name} for subcategory {category.Name}");
                    return mainCategory;
                }
                else
                {
                    // إذا كانت فئة رئيسية، أرجعها هي
                    LogInfo($"Category {category?.Name} is a main category");
                    return category;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting main category for {categoryId}", ex);
                return null;
            }
        }

        // ✅ دالة تجيب اسم المنتج من Airtable بالـ Record ID
        public async Task<string> GetProductNameByIdAsync(string recordId)
        {
            try
            {
                var response = await _airtableBase.RetrieveRecord(_productsTableName, recordId);

                if (response.Success && response.Record.Fields.ContainsKey("Name"))
                {
                    var productName = response.Record.Fields["Name"]?.ToString() ?? "اسم غير معروف";
                    LogInfo($"Retrieved product name: {productName} for ID: {recordId}");
                    return productName;
                }
                else
                {
                    LogError($"Failed to retrieve product name for ID: {recordId}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error getting product name for ID: {recordId}", ex);
            }

            return "اسم غير معروف";
        }

        // ✅ إنشاء Reservation جديد
        public async Task<string> CreateReservationAsync(CreateReservationDto dto)
        {
            if (string.IsNullOrEmpty(dto.ProductRecordId))
                throw new Exception("ProductRecordId is required and must be a valid Airtable record ID.");

            try
            {
                var fields = new Fields();
                fields.AddField("Product", new string[] { dto.ProductRecordId });
                fields.AddField("Quantity Meters", dto.QuantityMeters);
                fields.AddField("Customer Name", dto.CustomerName);
                fields.AddField("Customer Phone", dto.CustomerPhone);
                fields.AddField("Customer Address", dto.CustomerAddress);

                var response = await _airtableBase.CreateRecord("Reservations", fields);

                if (response.Success)
                {
                    var productName = await GetProductNameByIdAsync(dto.ProductRecordId);

                    var msg = $"🧾 حجز جديد!\n" +
                              $"📦 المنتج: {productName}\n" +
                              $"📏 الكمية: {dto.QuantityMeters} متر\n" +
                              $"👤 الاسم: {dto.CustomerName}\n" +
                              $"📞 الموبايل: {dto.CustomerPhone}\n" +
                              $"📍 العنوان: {dto.CustomerAddress}";

                    await _telegram.SendMessageAsync(msg);
                    LogInfo($"✅ Reservation created successfully for product: {productName}");

                    return response.Record.Id;
                }
                else
                {
                    var errorMsg = response.AirtableApiError?.ErrorMessage ?? "Unknown error";
                    var detailedMsg = response.AirtableApiError?.DetailedErrorMessage ?? "No details";
                    LogError($"Airtable error creating reservation: {errorMsg}. Details: {detailedMsg}");
                    throw new Exception($"Airtable error: {errorMsg}. Details: {detailedMsg}");
                }
            }
            catch (Exception ex)
            {
                LogError("Error creating reservation", ex);
                throw;
            }
        }

        // ✅ دالة إضافية لفحص هيكل التصنيفات (مفيدة للديباج)
        public async Task<object> GetCategoriesStructureAsync()
        {
            try
            {
                var allCategories = await GetAllCategoriesAsync();
                var mainCategories = allCategories.Where(c => c.IsMainCategory).ToList();

                var structure = new
                {
                    TotalCategories = allCategories.Count,
                    MainCategoriesCount = mainCategories.Count,
                    SubCategoriesCount = allCategories.Count - mainCategories.Count,
                    MainCategories = mainCategories.Select(mc => new
                    {
                        Id = mc.Id,
                        Name = mc.Name,
                        ProductsCount = mc.ProductsCount,
                        SubCategories = allCategories
                            .Where(sc => sc.ParentCategory.Contains(mc.Id))
                            .Select(sc => new
                            {
                                Id = sc.Id,
                                Name = sc.Name,
                                ProductsCount = sc.ProductsCount
                            })
                    })
                };

                LogInfo($"Categories structure: {structure.MainCategoriesCount} main, {structure.SubCategoriesCount} sub");
                return structure;
            }
            catch (Exception ex)
            {
                LogError("Error getting categories structure", ex);
                return new { Error = "Failed to get categories structure" };
            }
        }
    }
}