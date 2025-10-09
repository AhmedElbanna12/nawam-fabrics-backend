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

                var categoriesResponse = await airtableBase.ListRecords("Categories");
                var categoriesDict = new Dictionary<string, string>();

                if (categoriesResponse.Success)
                {
                    Console.WriteLine("=== Categories ===");
                    foreach (var cat in categoriesResponse.Records)
                    {
                        var categoryName = cat.GetField<string>("Name");
                        categoriesDict[cat.Id] = categoryName;
                        Console.WriteLine($"ID: {cat.Id} -> Name: {categoryName}");
                    }
                }

                var response = await airtableBase.ListRecords("Products");
                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        try
                        {
                            var mainCategoryIds = record.GetField<string[]>("Main Category");
                            var subCategoryIds = record.GetField<string[]>("Sub Category");

                            Console.WriteLine($"\n=== Product: {record.GetField("Name")} ===");
                            Console.WriteLine($"MainCategory IDs: {string.Join(", ", mainCategoryIds ?? new string[0])}");
                            Console.WriteLine($"SubCategory IDs: {string.Join(", ", subCategoryIds ?? new string[0])}");

                            var mainCategoryId = mainCategoryIds?.FirstOrDefault();
                            var subCategoryId = subCategoryIds?.FirstOrDefault();

                            Console.WriteLine($"Looking for MainCategory ID: {mainCategoryId}");
                            Console.WriteLine($"Found in dict: {(mainCategoryId != null && categoriesDict.ContainsKey(mainCategoryId))}");

                            var mainCategoryName = mainCategoryId != null && categoriesDict.ContainsKey(mainCategoryId)
                                                  ? categoriesDict[mainCategoryId]
                                                  : null;
                            var subCategoryName = subCategoryId != null && categoriesDict.ContainsKey(subCategoryId)
                                                 ? categoriesDict[subCategoryId]
                                                 : null;

                            var product = new Dictionary<string, object>
                            {
                                ["Id"] = record.Id,
                                ["Name"] = record.GetField("Name"),
                                ["PricePerMeter"] = record.GetField("PricePerMeter"),
                                ["Description"] = record.GetField("Description"),
                                ["MainCategory"] = mainCategoryName,
                                ["SubCategory"] = subCategoryName
                            };

                            products.Add(product);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing record {record.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
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


