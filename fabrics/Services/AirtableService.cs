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




        private AirtableBase GetBase() => new AirtableBase(_apiKey, _baseId);


        public async Task<(List<Category> parents, List<Category> subs)> GetCategoriesAsync()
        {
            var categories = await GetAllAsync<Category>("Categories");
            var parents = categories.Where(c => c.ParentCategoryIds == null || c.ParentCategoryIds.Count == 0).ToList();
            var subs = categories.Where(c => c.ParentCategoryIds != null && c.ParentCategoryIds.Count > 0).ToList();

            return (parents, subs);
        }


        // جلب كل المنتجات

        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            var categories = new List<Category>();

            try
            {
                var response = await _airtableBase.ListRecords(_categoriesTableName);

                if (response.Success)
                {
                    foreach (var record in response.Records)
                    {
                        var fields = record.Fields;

                        var category = new Category
                        {
                            Id = record.Id, // ✅ مهم جدًا
                            Name = fields.ContainsKey("Name") ? fields["Name"]?.ToString() : null,
                            ParentCategory = fields.ContainsKey("Parent Category")
                                ? ((Newtonsoft.Json.Linq.JArray)fields["Parent Category"]).ToObject<string[]>()
                                : null
                        };

                        categories.Add(category);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fetching categories: {ex.Message}");
            }

            return categories;
        }

        public async Task<List<Category>> GetMainCategoriesAsync()
        {
            var allCategories = await GetAllCategoriesAsync();
            return allCategories.Where(c => c.IsMainCategory).ToList();
        }

        public async Task<List<Category>> GetSubCategoriesAsync(string parentCategoryId)
        {
            var allCategories = await GetAllCategoriesAsync();
            return allCategories.Where(c => c.ParentCategory != null &&
                                   c.ParentCategory.Contains(parentCategoryId)).ToList();
        }

        public async Task<List<Product>> GetProductsByCategoryAsync(string categoryId)
        {
            var products = new List<Product>();

            // Build formula to filter products by category
            var formula = $"{{Category}} = '{categoryId}'";

            try
            {
                var response = await _airtableBase.ListRecords<Product>(_productsTableName, formula);
                products = response.Records.Select(r => r.Fields).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching products: {ex.Message}");
            }

            return products;
        }

        //public async Task<List<Dictionary<string, object>>> GetProductsAsync()
        //{
        //    var products = new List<Dictionary<string, object>>();
        //    using var airtableBase = GetBase();

        //    var response = await airtableBase.ListRecords("Products");

        //    if (response.Success)
        //    {
        //        foreach (var record in response.Records)
        //        {
        //            var product = new Dictionary<string, object>
        //            {
        //                ["Id"] = record.Id,
        //                ["Name"] = record.GetField<string>("Name"),
        //                ["PricePerMeter"] = record.GetField<double?>("PricePerMeter"),
        //                ["MainCategoryId"] = record.GetField<List<string>>("MainCategory")?.FirstOrDefault(),
        //                ["SubCategoryId"] = record.GetField<List<string>>("SubCategory")?.FirstOrDefault()
        //            };
        //            products.Add(product);
        //        }
        //    }

        //    return products;
        //}
    

        //public async Task<(List<AirtableCategory> parents, List<AirtableCategory> subs)> GetCategoriesAsync()
        //{
        //    var categories = new List<AirtableCategory>();
        //    using var airtableBase = GetBase();

        //    var response = await airtableBase.ListRecords("Categories");

        //    if (response.Success)
        //    {
        //        foreach (var record in response.Records)
        //        {
        //            var category = new AirtableCategory
        //            {
        //                Id = record.Id,
        //                Name = record.GetField<string>("Name"),
        //                ParentCategoryIds = record.GetField<List<string>>("ParentCategory")
        //            };
        //            categories.Add(category);
        //        }
        //    }

        //    var parentCategories = categories
        //        .Where(c => c.ParentCategoryIds == null || c.ParentCategoryIds.Count == 0)
        //        .ToList();

        //    var subCategories = categories
        //        .Where(c => c.ParentCategoryIds != null && c.ParentCategoryIds.Count > 0)
        //        .ToList();

        //    return (parentCategories, subCategories);
        //}


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


