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


        public AirtableService(IConfiguration config , TelegramService telegram)
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
            using var airtableBase = GetBase();
            var response = await airtableBase.ListRecords("Products");

            if (response.Success)
            {
                foreach (var record in response.Records)
                {
                    var product = new Dictionary<string, object>
                    {
                        ["Id"] = record.Id,
                        ["Name"] = record.GetField("Name"),
                        ["PricePerMeter"] = record.GetField("PricePerMeter"),
                        ["Description"] = record.GetField("Description")
                    };
                    products.Add(product);
                }
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
                        ["Parent Category"] = record.GetField("Parent Category")
                    };
                    categories.Add(cat);
                }
            }

            return categories;
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
                // 📩 ابعت رسالة لصاحب المحل بعد نجاح الحجز
                var msg = $"📦 حجز جديد!\n" +
                          $"المنتج: {dto.ProductRecordId}\n" +
                          $"الكمية: {dto.QuantityMeters} متر\n" +
                          $"الاسم: {dto.CustomerName}\n" +
                          $"الموبايل: {dto.CustomerPhone}\n" +
                          $"العنوان: {dto.CustomerAddress}";

                await _telegram.SendMessageAsync(msg);

                return response.Record.Id;
            }

            // Log the detailed error for debugging
            var errorMsg = response.AirtableApiError?.ErrorMessage ?? "Unknown error";
            var detailedMsg = response.AirtableApiError?.DetailedErrorMessage ?? "No details";
            throw new Exception($"Airtable error: {errorMsg}. Details: {detailedMsg}");
        }
    }
}


