using AirtableApiClient;
using fabrics.Dtos;
using fabrics.Models;
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
        public async Task<List<product>> GetProductsAsync()
        {
            var products = new List<product>();
            using var airtableBase = GetBase();
            var response = await airtableBase.ListRecords("Products");

            if (response.Success)
            {
                foreach (var record in response.Records)
                {
                    var product = new product
                    {
                        Id = record.Id,
                        Name = record.GetField<string>("Name"),
                        Description = record.GetField<string>("Description"),
                        PricePerMeter = record.GetField<double?>("PricePerMeter"),
                        SubCategory = record.GetField<string[]>("Sub Category")?.FirstOrDefault(),
                        MainCategory = record.GetField<string[]>("Main Category")?.FirstOrDefault()
                    };
                    products.Add(product);
                }
            }

            return products;
        }


        public async Task<List<Category>> GetCategoriesAsync()
        {
            var categories = new List<Category>();
            using var airtableBase = GetBase();
            var response = await airtableBase.ListRecords("Categories");

            if (response.Success)
            {
                foreach (var record in response.Records)
                {
                    var category = new Category
                    {
                        Id = record.Id,
                        Name = record.GetField<string>("Name"),
                        Description = record.GetField<string>("Description"),
                        ParentCategory = record.GetField<string[]>("ParentCategory")?.FirstOrDefault()
                    };
                    categories.Add(category);
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


