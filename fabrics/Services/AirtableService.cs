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




        public AirtableService( IConfiguration config, TelegramService telegram )
            { 

            _telegram = telegram;
            _apiKey = config["Airtable:ApiKey"];
            _baseId = config["Airtable:BaseId"];
            _categoriesTableName = "Categories";
            _productsTableName = "Products";
            _airtableBase = new AirtableBase(_apiKey, _baseId);

        }




        //private AirtableBase GetBase() => new AirtableBase(_apiKey, _baseId);



        // جلب كل الفئات
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            var categories = new List<Category>();

            try
            {
                var response = await _airtableBase.ListRecords(_categoriesTableName);

                if (response.Success)
                {
                    Console.WriteLine("=== RAW CATEGORIES DATA ===");
                    foreach (var record in response.Records)
                    {
                        // ✅ التعديل هنا: معالجة ParentCategory كمصفوفة أو كقيمة مفردة
                        var parentCategoryField = record.GetField<object>("ParentCategory");
                        string[] parentCategory = null;

                        if (parentCategoryField != null)
                        {
                            if (parentCategoryField is JsonElement jsonElement)
                            {
                                if (jsonElement.ValueKind == JsonValueKind.Array)
                                {
                                    parentCategory = jsonElement.EnumerateArray()
                                        .Select(e => e.GetString())
                                        .ToArray();
                                }
                                else if (jsonElement.ValueKind == JsonValueKind.String)
                                {
                                    parentCategory = new[] { jsonElement.GetString() };
                                }
                            }
                            else if (parentCategoryField is string singleValue)
                            {
                                parentCategory = new[] { singleValue };
                            }
                        }

                        Console.WriteLine($"ID: {record.Id}, Name: {record.GetField<string>("Name")}, ParentCategory: {(parentCategory == null ? "NULL" : $"[{string.Join(",", parentCategory)}]")}");

                        var category = new Category
                        {
                            Id = record.Id,
                            Name = record.GetField<string>("Name") ?? "غير معروف",
                            ParentCategory = parentCategory ?? new string[0]
                        };
                        categories.Add(category);
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Airtable Error: {response.AirtableApiError?.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching categories: {ex.Message}");
            }

            return categories;
        }

        // ✅ جلب الفئات الرئيسية فقط (التي ليس لها ParentCategory)
        public async Task<List<Category>> GetMainCategoriesAsync()
        {
            var allCategories = await GetAllCategoriesAsync();

            // الفئات الرئيسية هي التي ليس لها ParentCategory
            var mainCategories = allCategories.Where(c =>
                c.ParentCategory == null ||
                c.ParentCategory.Length == 0 ||
                c.ParentCategory.All(string.IsNullOrEmpty)
            ).ToList();

            Console.WriteLine($"=== Main Categories Found: {mainCategories.Count} ===");
            foreach (var cat in mainCategories)
            {
                Console.WriteLine($"Main Category: {cat.Name}, ID: {cat.Id}");
            }

            return mainCategories;
        }

        // ✅ جلب الفئات الفرعية الخاصة بفئة رئيسية معينة
        public async Task<List<Category>> GetSubCategoriesAsync(string parentCategoryId)
        {
            var allCategories = await GetAllCategoriesAsync();

            // الفئات الفرعية هي التي تحتوي على parentCategoryId في مصفوفة ParentCategory
            var subCategories = allCategories.Where(c =>
                c.ParentCategory != null &&
                c.ParentCategory.Contains(parentCategoryId)
            ).ToList();

            Console.WriteLine($"=== SubCategories for {parentCategoryId} ===");
            foreach (var sub in subCategories)
            {
                Console.WriteLine($"SubCategory: {sub.Name}, ID: {sub.Id}");
            }

            return subCategories;
        }

        // ✅ جلب الفئة الرئيسية من الفئة الفرعية
        public async Task<Category> GetMainCategoryFromSubCategoryAsync(string subCategoryId)
        {
            var allCategories = await GetAllCategoriesAsync();
            var subCategory = allCategories.FirstOrDefault(c => c.Id == subCategoryId);

            if (subCategory?.ParentCategory?.FirstOrDefault() != null)
            {
                var mainCategoryId = subCategory.ParentCategory[0];
                return allCategories.FirstOrDefault(c => c.Id == mainCategoryId);
            }

            return null;
        }

        // ✅ جلب المنتجات الخاصة بفئة معينة (سواء كانت رئيسية أو فرعية)
        public async Task<List<Product>> GetProductsByCategoryAsync(string categoryId)
        {
            var products = new List<Product>();

            try
            {
                // نستخدم Lookup field للعثور على المنتجات المرتبطة بهذه الفئة
                var formula = $"OR({{Category}} = '{categoryId}', FIND('{categoryId}', {{Category}}))";
                var response = await _airtableBase.ListRecords(_productsTableName, filterByFormula: formula);

                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        var product = new Product
                        {
                            Id = record.Id,
                            Name = record.GetField<string>("Name") ?? "غير معروف",
                            Description = record.GetField<string>("Description") ?? "",
                            PricePerMeter = record.GetField<decimal?>("PricePerMeter") ?? 0,
                            Image = record.GetField<string>("Image") ?? "",
                            Category = record.GetField<string[]>("Category") ?? new string[0]
                        };

                        // ✅ إضافة المنطق لتحديد MainCategory و SubCategory تلقائياً
                        if (product.Category.Length > 0)
                        {
                            var firstCategoryId = product.Category[0];
                            var firstCategory = (await GetAllCategoriesAsync())
                                .FirstOrDefault(c => c.Id == firstCategoryId);

                            if (firstCategory != null)
                            {
                                // إذا كانت الفئة رئيسية
                                if (firstCategory.ParentCategory == null || firstCategory.ParentCategory.Length == 0)
                                {
                                    product.MainCategory = new[] { firstCategoryId };
                                    product.SubCategory = new string[0];
                                }
                                else // إذا كانت الفئة فرعية
                                {
                                    product.SubCategory = new[] { firstCategoryId };
                                    product.MainCategory = firstCategory.ParentCategory;
                                }
                            }
                        }

                        products.Add(product);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching products: {ex.Message}");
            }

            return products;
        }

        // ✅ جلب المنتجات الخاصة بفئة رئيسية (بما في ذلك منتجات الفئات الفرعية التابعة لها)
        public async Task<List<Product>> GetProductsByMainCategoryAsync(string mainCategoryId)
        {
            var allProducts = new List<Product>();

            // 1. جلب المنتجات المرتبطة مباشرة بالفئة الرئيسية
            var directProducts = await GetProductsByCategoryAsync(mainCategoryId);
            allProducts.AddRange(directProducts);

            // 2. جلب الفئات الفرعية التابعة لهذه الفئة الرئيسية
            var subCategories = await GetSubCategoriesAsync(mainCategoryId);

            // 3. جلب منتجات كل فئة فرعية
            foreach (var subCategory in subCategories)
            {
                var subProducts = await GetProductsByCategoryAsync(subCategory.Id);
                allProducts.AddRange(subProducts);
            }

            return allProducts.DistinctBy(p => p.Id).ToList();
        }


















        // ✅ دالة تجيب اسم المنتج من Airtable بالـ Record ID
        public async Task<string> GetProductNameByIdAsync(string recordId)
        {
            try
            {
                // ✅ استخدم _airtableBase مباشرة
                var response = await _airtableBase.RetrieveRecord("Products", recordId);

                if (response.Success && response.Record.Fields.ContainsKey("Name"))
                {
                    return response.Record.Fields["Name"]?.ToString() ?? "اسم غير معروف";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting product name: {ex.Message}");
            }

            return "اسم غير معروف";
        }



        // إنشاء Reservation جديد
        public async Task<string> CreateReservationAsync(CreateReservationDto dto)
        {
            if (string.IsNullOrEmpty(dto.ProductRecordId))
                throw new Exception("ProductRecordId is required and must be a valid Airtable record ID.");


            var fields = new Fields();
            fields.AddField("Product", new string[] { dto.ProductRecordId });
            fields.AddField("Quantity Meters", dto.QuantityMeters);
            fields.AddField("Customer Name", dto.CustomerName);
            fields.AddField("Customer Phone", dto.CustomerPhone);
            fields.AddField("Customer Address", dto.CustomerAddress);

            var response = await _airtableBase.CreateRecord("Reservations", fields);

            if (response.Success)
            {
                // 🟢 1. هات اسم المنتج بدل الـ ID
                var productName = await GetProductNameByIdAsync(dto.ProductRecordId);

                // 🟢 2. جهّز الرسالة بشكل منسق
                var msg = $"🧾 حجز جديد!\n" +
                          $"📦 المنتج: {productName}\n" +
                          $"📏 الكمية: {dto.QuantityMeters} متر\n" +
                          $"👤 الاسم: {dto.CustomerName}\n" +
                          $"📞 الموبايل: {dto.CustomerPhone}\n" +
                          $"📍 العنوان: {dto.CustomerAddress}";

                // 🟢 3. ابعت الإشعار لتليجرام
                await _telegram.SendMessageAsync(msg);

                return response.Record.Id;
            }

            var errorMsg = response.AirtableApiError?.ErrorMessage ?? "Unknown error";
            var detailedMsg = response.AirtableApiError?.DetailedErrorMessage ?? "No details";
            throw new Exception($"Airtable error: {errorMsg}. Details: {detailedMsg}");
        }
    }
}


