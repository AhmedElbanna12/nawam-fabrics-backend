using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace fabrics.Services
{
    public class MessengerService
    {
        private readonly string _pageAccessToken;
        private readonly AirtableService _airtable;
        private readonly IHttpClientFactory _httpClientFactory;

        public MessengerService(IConfiguration config, AirtableService airtable, IHttpClientFactory httpClientFactory)
        {
            _pageAccessToken = config["Messenger:PageAccessToken"];
            _airtable = airtable;
            _httpClientFactory = httpClientFactory;
        }

        // ✅ الدالة الأساسية لمعالجة الرسائل
        public async Task HandleMessageAsync(JsonElement body)
        {
            try
            {
                var entry = body.GetProperty("entry")[0];
                var messaging = entry.GetProperty("messaging")[0];
                var senderId = messaging.GetProperty("sender").GetProperty("id").GetString();

                if (messaging.TryGetProperty("message", out var messageObj))
                {
                    // أول رسالة من العميل → نرد برسالة ترحيب
                    var text = messageObj.GetProperty("text").GetString();
                    if (text != null)
                    {
                        await SendTextMessageAsync(senderId, "  أهلاً بيك في متجرنا! اضغط على الزرار اللي يناسبك عشان تشوف منتجاتنا واسعارنا ");
                        await ShowMainCategoriesAsync(senderId);
                    }
                }
                else if (messaging.TryGetProperty("postback", out var postbackObj))
                {
                    var payload = postbackObj.GetProperty("payload").GetString();
                    await HandlePostbackAsync(senderId, payload);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Messenger Error: {ex.Message}");
            }
        }

        // ✅ الرد برسالة نصية بسيطة
        public async Task SendTextMessageAsync(string recipientId, string text)
        {
            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text }
            };

            await SendRequestAsync(payload);
        }

        // ✅ عرض الأزرار الخاصة بالمنتجات الرئيسية
        public async Task ShowMainCategoriesAsync(string recipientId)
        {
            var categories = await _airtable.GetCategoriesAsync();

            // هنجيب فقط الكاتيجوري اللي مفيهاش "Parent Category" (يعني main)
            var mainCategories = categories
.Where(c => !c.ContainsKey("ParentCategory"))
                .Take(6) // نعرض 3 أزرار كحد أقصى
                .Select(c => new
                {
                    type = "postback",
                    title = c["Name"].ToString(),
                    payload = $"MAIN_{c["Id"]}"
                })
                .ToList();

            var payload = new
            {
                recipient = new { id = recipientId },
                message = new
                {
                    attachment = new
                    {
                        type = "template",
                        payload = new
                        {
                            template_type = "button",
                            text = "اختار الفئة الرئيسية:",
                            buttons = mainCategories
                        }
                    }
                }
            };

            await SendRequestAsync(payload);
        }

        // ✅ عرض الـ SubCategories عند الضغط على MainCategory
        public async Task ShowSubCategoriesAsync(string recipientId, string mainCategoryId)
        {
            var categories = await _airtable.GetCategoriesAsync();

            var subCategories = categories
               .Where(c =>
    c.TryGetValue("ParentCategory", out var parentObj) &&
    parentObj is string[] parentArr &&
    parentArr.Contains(mainCategoryId))

                .Select(c => new
                {
                    type = "postback",
                    title = c["Name"].ToString(),
                    payload = $"SUB_{c["Id"]}"
                })
                .ToList();

            if (subCategories.Any())
            {
                var payload = new
                {
                    recipient = new { id = recipientId },
                    message = new
                    {
                        attachment = new
                        {
                            type = "template",
                            payload = new
                            {
                                template_type = "button",
                                text = "اختار الفئة الفرعية:",
                                buttons = subCategories.Take(4)
                            }
                        }
                    }
                };
                await SendRequestAsync(payload);
            }
            else
            {
                await SendTextMessageAsync(recipientId, "❌ لا يوجد فئات فرعية لهذه الفئة.");
            }
        }

        // ✅ عرض المنتجات بناءً على SubCategory
        public async Task ShowProductsAsync(string recipientId, string subCategoryId)
        {
            var products = await _airtable.GetProductsAsync();
            var filtered = products.Where(p => (string)p["subCategory"] == subCategoryId).ToList();

            if (!filtered.Any())
            {
                await SendTextMessageAsync(recipientId, "❌ لا يوجد منتجات في هذه الفئة.");
                return;
            }

            foreach (var p in filtered)
            {
                var text = $"📦 {p["Name"]}\n💰 السعر: {p["PricePerMeter"]} جنيه\n📝 {p["Description"]}";
                await SendTextMessageAsync(recipientId, text);
            }
        }

        // ✅ معالجه postback
        private async Task HandlePostbackAsync(string senderId, string payload)
        {
            if (payload.StartsWith("MAIN_"))
            {
                var mainId = payload.Replace("MAIN_", "");
                await ShowSubCategoriesAsync(senderId, mainId);
            }
            else if (payload.StartsWith("SUB_"))
            {
                var subId = payload.Replace("SUB_", "");
                await ShowProductsAsync(senderId, subId);
            }
        }

        // ✅ دالة عامة لإرسال أي طلب إلى Graph API
        private async Task SendRequestAsync(object payload)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"https://graph.facebook.com/v20.0/me/messages?access_token={_pageAccessToken}";
            var response = await httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var respText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Messenger API Error: {respText}");
            }
        }
    }
}
