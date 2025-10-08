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

        // ✅ استقبال الرسائل
        public async Task HandleMessageAsync(JsonElement body)
        {
            try
            {
                var entry = body.GetProperty("entry")[0];
                var messaging = entry.GetProperty("messaging")[0];
                var senderId = messaging.GetProperty("sender").GetProperty("id").GetString();

                if (messaging.TryGetProperty("message", out var messageObj))
                {
                    var text = messageObj.GetProperty("text").GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        await SendTextMessageAsync(senderId, "👋 أهلاً بيك في متجرنا! اختار الفئة اللي تناسبك:");
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

        // ✅ إرسال نص
        public async Task SendTextMessageAsync(string recipientId, string text)
        {
            var payload = new
            {
                recipient = new { id = recipientId },
                message = new { text }
            };
            await SendRequestAsync(payload);
        }

        // ✅ عرض الكاتيجوريات الرئيسية (بحد أقصى 3 في الرسالة الواحدة)
        public async Task ShowMainCategoriesAsync(string recipientId)
        {
            var categories = await _airtable.GetCategoriesAsync();

            // ✅ نعرض فقط الكاتيجوري اللي ParentCategory = null أو مش موجودة
            var mainCategories = categories
                .Where(c =>
                    !c.ContainsKey("ParentCategory") ||
                    c["ParentCategory"] == null ||
                    (c["ParentCategory"] is string[] arr && arr.Length == 0)
                )
                .ToList();

            if (!mainCategories.Any())
            {
                await SendTextMessageAsync(recipientId, "❌ لا يوجد فئات رئيسية حالياً.");
                return;
            }

            // ✅ نقسمهم مجموعات كل مجموعة فيها 3 أزرار (لأن الماسنجر بيسمح بـ 3 فقط)
            var buttonGroups = mainCategories
                .Select((c, i) => new { c, i })
                .GroupBy(x => x.i / 3)
                .Select(g => g.Select(x => new
                {
                    type = "postback",
                    title = x.c["Name"].ToString(),
                    payload = $"MAIN_{x.c["Id"]}"
                }).ToList())
                .ToList();

            foreach (var group in buttonGroups)
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
                                text = "📂 اختار الفئة الرئيسية:",
                                buttons = group
                            }
                        }
                    }
                };

                await SendRequestAsync(payload);
                await Task.Delay(500); // تأخير بسيط بين الرسائل عشان الماسنجر ما يحجبهمش
            }
        }

        public async Task ShowSubCategoriesAsync(string recipientId, string mainCategoryId)
        {
            var categories = await _airtable.GetCategoriesAsync();

            // ✅ نعرض فقط الفئات اللي ParentCategory = mainCategoryId
            var subCategories = categories
                .Where(c =>
                    c.ContainsKey("ParentCategory") &&
                    c["ParentCategory"] is string[] parentArr &&
                    parentArr.Contains(mainCategoryId)
                )
                .ToList();

            if (!subCategories.Any())
            {
                await SendTextMessageAsync(recipientId, "❌ لا يوجد فئات فرعية لهذه الفئة.");
                return;
            }

            // ✅ نقسمهم مجموعات كل مجموعة فيها 3 أزرار
            var buttonGroups = subCategories
                .Select((c, i) => new { c, i })
                .GroupBy(x => x.i / 3)
                .Select(g => g.Select(x => new
                {
                    type = "postback",
                    title = x.c["Name"].ToString(),
                    payload = $"SUB_{x.c["Id"]}"
                }).ToList())
                .ToList();

            foreach (var group in buttonGroups)
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
                                text = "📁 اختار الفئة الفرعية:",
                                buttons = group
                            }
                        }
                    }
                };

                await SendRequestAsync(payload);
                await Task.Delay(500); // تأخير بسيط بين الرسائل
            }
        }

        // ✅ عرض المنتجات (بكروت)
        public async Task ShowProductsAsync(string recipientId, string subCategoryId)
        {
            var products = await _airtable.GetProductsAsync();

            var filtered = products
                .Where(p =>
                    p.TryGetValue("subCategory", out var subObj) &&
                    subObj is string[] subArr &&
                    subArr.Contains(subCategoryId))
                .ToList();

            if (!filtered.Any())
            {
                await SendTextMessageAsync(recipientId, "❌ لا يوجد منتجات في هذه الفئة.");
                return;
            }

            var elements = filtered.Select(p => new
            {
                title = p["Name"].ToString(),
                subtitle = $"💰 السعر: {p["PricePerMeter"]} جنيه",
                image_url = p.ContainsKey("Image") && p["Image"] is string[] imgs && imgs.Length > 0
                    ? imgs[0]
                    : "https://via.placeholder.com/400x300.png?text=No+Image",
                buttons = new[]
                {
                    new { type = "postback", title = "📄 تفاصيل", payload = $"DETAIL_{p["Id"]}" }
                }
            }).ToList();

            // Messenger يسمح بـ 10 كروت في الرسالة
            foreach (var group in elements.Chunk(10))
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
                                template_type = "generic",
                                elements = group
                            }
                        }
                    }
                };

                await SendRequestAsync(payload);
            }
        }

        // ✅ معالجة postback
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
            else if (payload.StartsWith("DETAIL_"))
            {
                var productId = payload.Replace("DETAIL_", "");
                var products = await _airtable.GetProductsAsync();
                var product = products.FirstOrDefault(p => p["Id"].ToString() == productId);

                if (product != null)
                {
                    var details = $"📦 {product["Name"]}\n💰 السعر: {product["PricePerMeter"]} جنيه\n📝 {product["Description"]}";
                    await SendTextMessageAsync(senderId, details);
                }
            }
        }

        // ✅ إرسال الطلب
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
