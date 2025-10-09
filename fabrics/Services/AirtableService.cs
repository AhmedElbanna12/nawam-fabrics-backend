using AirtableApiClient;
using fabrics.Dtos;
using fabrics.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace fabrics.Services
{
    public class AirtableService
    {
        private readonly string _apiKey;
        private readonly string _baseId;
        private readonly TelegramService _telegram;


        public AirtableService(IConfiguration config, TelegramService telegram)
        {
            _apiKey = config["Airtable:ApiKey"];
            _baseId = config["Airtable:BaseId"];
            _telegram = telegram;

        }

        private AirtableBase GetBase() => new AirtableBase(_apiKey, _baseId);

        // جلب كل المنتجات
        public async Task<List<Dictionary<string, object>>> GetProductsAsync()
        {
            var products = new List<Dictionary<string, object>>();
            try
            {
                using var airtableBase = GetBase();

                // 1️⃣ جلب Categories
                var categoriesResponse = await airtableBase.ListRecords("Categories");
                var categoriesDict = new Dictionary<string, string>();

                if (categoriesResponse.Success)
                {
                    Console.WriteLine("=== Categories Loaded ===");
                    foreach (var cat in categoriesResponse.Records)
                    {
                        var catName = cat.GetField<string>("Name");
                        categoriesDict[cat.Id] = catName;
                        Console.WriteLine($"ID: '{cat.Id}' -> Name: '{catName}'");
                    }
                    Console.WriteLine($"\nTotal: {categoriesDict.Count} categories\n");
                }

                // 2️⃣ جلب Products
                var response = await airtableBase.ListRecords("Products");

                if (response.Success)
                {
                    Console.WriteLine("=== Products ===");
                    foreach (var record in response.Records)
                    {
                        try
                        {
                            var productName = record.GetField<string>("Name");
                            Console.WriteLine($"\n📦 Product: {productName}");

                            // طباعة RAW data من Airtable
                            if (record.Fields.ContainsKey("MainCategory"))
                            {
                                var rawMain = record.Fields["MainCategory"];
                                Console.WriteLine($"MainCategory RAW Type: {rawMain?.GetType().FullName ?? "null"}");
                                Console.WriteLine($"MainCategory RAW Value: {System.Text.Json.JsonSerializer.Serialize(rawMain)}");
                            }
                            else
                            {
                                Console.WriteLine("⚠️ MainCategory field NOT FOUND in record.Fields");
                            }

                            if (record.Fields.ContainsKey("SubCategory"))
                            {
                                var rawSub = record.Fields["SubCategory"];
                                Console.WriteLine($"SubCategory RAW Type: {rawSub?.GetType().FullName ?? "null"}");
                                Console.WriteLine($"SubCategory RAW Value: {System.Text.Json.JsonSerializer.Serialize(rawSub)}");
                            }
                            else
                            {
                                Console.WriteLine("⚠️ SubCategory field NOT FOUND in record.Fields");
                            }

                            // جرب قراءة بطرق مختلفة
                            List<string> mainCategoryIds = null;
                            List<string> subCategoryIds = null;

                            // محاولة 1: List<string>
                            try
                            {
                                mainCategoryIds = record.GetField<List<string>>("MainCategory");
                                Console.WriteLine($"✓ Read as List<string>: {mainCategoryIds?.Count ?? 0} items");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"✗ List<string> failed: {ex.Message}");

                                // محاولة 2: string[]
                                try
                                {
                                    var arr = record.GetField<string[]>("MainCategory");
                                    mainCategoryIds = arr?.ToList();
                                    Console.WriteLine($"✓ Read as string[]: {mainCategoryIds?.Count ?? 0} items");
                                }
                                catch (Exception ex2)
                                {
                                    Console.WriteLine($"✗ string[] failed: {ex2.Message}");

                                    // محاولة 3: dynamic/object
                                    try
                                    {
                                        if (record.Fields.ContainsKey("MainCategory"))
                                        {
                                            var obj = record.Fields["MainCategory"];
                                            if (obj is System.Text.Json.JsonElement jsonElement)
                                            {
                                                Console.WriteLine($"It's a JsonElement! Kind: {jsonElement.ValueKind}");
                                                if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                                                {
                                                    mainCategoryIds = new List<string>();
                                                    foreach (var item in jsonElement.EnumerateArray())
                                                    {
                                                        mainCategoryIds.Add(item.GetString());
                                                    }
                                                    Console.WriteLine($"✓ Extracted from JsonElement: {mainCategoryIds.Count} items");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex3)
                                    {
                                        Console.WriteLine($"✗ JsonElement failed: {ex3.Message}");
                                    }
                                }
                            }

                            // نفس الشيء للـ SubCategory
                            try
                            {
                                subCategoryIds = record.GetField<List<string>>("SubCategory");
                                Console.WriteLine($"✓ SubCategory as List<string>: {subCategoryIds?.Count ?? 0} items");
                            }
                            catch
                            {
                                try
                                {
                                    var arr = record.GetField<string[]>("SubCategory");
                                    subCategoryIds = arr?.ToList();
                                    Console.WriteLine($"✓ SubCategory as string[]: {subCategoryIds?.Count ?? 0} items");
                                }
                                catch { }
                            }

                            // طباعة الـ IDs
                            if (mainCategoryIds != null && mainCategoryIds.Any())
                            {
                                Console.WriteLine($"MainCategory IDs found:");
                                foreach (var id in mainCategoryIds)
                                {
                                    var exists = categoriesDict.ContainsKey(id);
                                    var name = exists ? categoriesDict[id] : "NOT FOUND";
                                    Console.WriteLine($"  '{id}' -> {name} {(exists ? "✓" : "✗")}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("⚠️ No MainCategory IDs extracted");
                            }

                            if (subCategoryIds != null && subCategoryIds.Any())
                            {
                                Console.WriteLine($"SubCategory IDs found:");
                                foreach (var id in subCategoryIds)
                                {
                                    var exists = categoriesDict.ContainsKey(id);
                                    var name = exists ? categoriesDict[id] : "NOT FOUND";
                                    Console.WriteLine($"  '{id}' -> {name} {(exists ? "✓" : "✗")}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("⚠️ No SubCategory IDs extracted");
                            }

                            // استخراج الأسماء
                            var mainCategoryName = mainCategoryIds?.FirstOrDefault() != null &&
                                                  categoriesDict.ContainsKey(mainCategoryIds.First())
                                                  ? categoriesDict[mainCategoryIds.First()]
                                                  : null;

                            var subCategoryName = subCategoryIds?.FirstOrDefault() != null &&
                                                 categoriesDict.ContainsKey(subCategoryIds.First())
                                                 ? categoriesDict[subCategoryIds.First()]
                                                 : null;

                            Console.WriteLine($"RESULT -> Main: '{mainCategoryName}', Sub: '{subCategoryName}'");

                            var product = new Dictionary<string, object>
                            {
                                ["Id"] = record.Id,
                                ["Name"] = productName,
                                ["PricePerMeter"] = record.GetField<double?>("PricePerMeter"),
                                ["Description"] = record.GetField<string>("Description"),
                                ["MainCategory"] = mainCategoryName,
                                ["SubCategory"] = subCategoryName
                            };

                            products.Add(product);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
            }

            return products;
        }

        public async Task<List<Dictionary<string, object>>> GetCategoriesAsync()
        {
            var categories = new List<Dictionary<string, object>>();
            using var airtableBase = GetBase();
            var response = await airtableBase.ListRecords("Categories");

            if (response.Success)
            {
                foreach (var record in response.Records)
                {
                    var cat = new Dictionary<string, object>
                    {
                        ["Id"] = record.Id,
                        ["Name"] = record.GetField("Name"),
                        ["Description"] = record.GetField("Description"),
                        ["ParentCategory"] = record.GetField("ParentCategory")
                    };
                    categories.Add(cat);
                }
            }

            return categories;
        }

        // ✅ دالة تجيب اسم المنتج من Airtable بالـ Record ID
        public async Task<string> GetProductNameByIdAsync(string recordId)
        {
            using var airtableBase = GetBase();
            var response = await airtableBase.RetrieveRecord("Products", recordId);

            if (response.Success && response.Record.Fields.ContainsKey("Name"))
            {
                return response.Record.Fields["Name"]?.ToString() ?? "اسم غير معروف";
            }

            return "اسم غير معروف";
        }




        // إنشاء Reservation جديد
        public async Task<string> CreateReservationAsync(CreateReservationDto dto)
        {
            if (string.IsNullOrEmpty(dto.ProductRecordId))
                throw new Exception("ProductRecordId is required and must be a valid Airtable record ID.");

            using var airtableBase = GetBase();

            var fields = new Fields();
            fields.AddField("Product", new string[] { dto.ProductRecordId });
            fields.AddField("Quantity Meters", dto.QuantityMeters);
            fields.AddField("Customer Name", dto.CustomerName);
            fields.AddField("Customer Phone", dto.CustomerPhone);
            fields.AddField("Customer Address", dto.CustomerAddress);

            var response = await airtableBase.CreateRecord("Reservations", fields);

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


