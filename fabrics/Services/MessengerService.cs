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
                    // ✅ التعامل مع الرسائل النصية
                    if (messageObj.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            await SendTextMessageAsync(senderId, "👋 أهلاً بيك في متجر الأقمشة! اختار الفئة اللي تناسبك:");
                            await ShowMainCategoriesAsync(senderId);
                        }
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
                Console.WriteLine($"❌ Messenger Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
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

        // ✅ عرض الفئات الرئيسية (بحد أقصى 3 أزرار في الرسالة)
        public async Task ShowMainCategoriesAsync(string recipientId)
        {
            try
            {
                var categories = await _airtable.GetCategoriesAsync();

                // ✅ نجيب الكاتيجوري الرئيسية (اللي مالهاش ParentCategory)
                var mainCategories = categories
     .Where(c =>
     {
         if (!c.ContainsKey("ParentCategory"))
             return true;

         var parent = c["ParentCategory"];

         // لو array فاضية = رئيسية
         if (parent is string[] arr && arr.Length == 0)
             return true;

         if (parent is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array && jsonEl.GetArrayLength() == 0)
             return true;

         return false; // عندها parent = فرعية
     })
     .ToList();


                if (!mainCategories.Any())
                {
                    await SendTextMessageAsync(recipientId, "❌ عذراً، لا توجد فئات متاحة حالياً.");
                    return;
                }

                Console.WriteLine($"✅ Found {mainCategories.Count} main categories");

                // ✅ نقسمهم مجموعات (كل مجموعة 3 أزرار لأن Messenger بيسمح بـ 3 فقط)
                var buttonGroups = mainCategories
                    .Select((c, i) => new { Category = c, Index = i })
                    .GroupBy(x => x.Index / 3)
                    .Select(g => g.Select(x => new
                    {
                        type = "postback",
                        title = TruncateText(x.Category["Name"]?.ToString() ?? "غير معروف", 20),
                        payload = $"MAIN_{x.Category["Id"]}"
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
                    await Task.Delay(500); // تأخير بسيط
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ShowMainCategoriesAsync: {ex.Message}");
                await SendTextMessageAsync(recipientId, "❌ حدث خطأ أثناء تحميل الفئات");
            }
        }

        // ✅ عرض الفئات الفرعية
        public async Task ShowSubCategoriesAsync(string recipientId, string mainCategoryId)
        {
            try
            {
                var categories = await _airtable.GetCategoriesAsync();

                // ✅ نجيب الفئات الفرعية اللي تابعة للفئة الرئيسية
                var subCategories = categories
                    .Where(c =>
                    {
                        if (!c.ContainsKey("ParentCategory"))
                            return false;

                        var parent = c["ParentCategory"];

                        // التعامل مع string array
                        if (parent is string[] parentArr)
                            return parentArr.Contains(mainCategoryId);

                        // التعامل مع JsonElement
                        if (parent is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in jsonEl.EnumerateArray())
                            {
                                if (item.GetString() == mainCategoryId)
                                    return true;
                            }
                        }

                        return false;
                    })
                    .ToList();

                Console.WriteLine($"✅ Found {subCategories.Count} sub-categories for main category {mainCategoryId}");

                if (!subCategories.Any())
                {
                    // لو مفيش فئات فرعية، نعرض المنتجات مباشرة من الفئة الرئيسية
                    await ShowProductsAsync(recipientId, mainCategoryId, isMainCategory: true);
                    return;
                }

                // ✅ نقسمهم مجموعات (كل مجموعة 3 أزرار)
                var buttonGroups = subCategories
                    .Select((c, i) => new { Category = c, Index = i })
                    .GroupBy(x => x.Index / 3)
                    .Select(g => g.Select(x => new
                    {
                        type = "postback",
                        title = TruncateText(x.Category["Name"]?.ToString() ?? "غير معروف", 20),
                        payload = $"SUB_{x.Category["Id"]}"
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
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ShowSubCategoriesAsync: {ex.Message}");
                await SendTextMessageAsync(recipientId, "❌ حدث خطأ أثناء تحميل الفئات الفرعية");
            }
        }

        // ✅ عرض المنتجات (بكروت)
        public async Task ShowProductsAsync(string recipientId, string categoryId, bool isMainCategory = false)
        {
            try
            {
                var products = await _airtable.GetProductsAsync();

                List<Dictionary<string, object>> filtered;

                if (isMainCategory)
                {
                    // البحث في mainCategory
                    filtered = products
                        .Where(p =>
                        {
                            if (!p.ContainsKey("MainCategory"))
                                return false;

                            var mainCat = p["MainCategory"];

                            if (mainCat is string[] mainArr)
                                return mainArr.Contains(categoryId);

                            if (mainCat is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in jsonEl.EnumerateArray())
                                {
                                    if (item.GetString() == categoryId)
                                        return true;
                                }
                            }

                            return false;
                        })
                        .ToList();
                }
                else
                {
                    // البحث في subCategory
                    filtered = products
                        .Where(p =>
                        {
                            if (!p.ContainsKey("SubCategory"))
                                return false;

                            var subCat = p["SubCategory"];

                            if (subCat is string[] subArr)
                                return subArr.Contains(categoryId);

                            if (subCat is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in jsonEl.EnumerateArray())
                                {
                                    if (item.GetString() == categoryId)
                                        return true;
                                }
                            }

                            return false;
                        })
                        .ToList();
                }

                Console.WriteLine($"✅ Found {filtered.Count} products for category {categoryId}");

                if (!filtered.Any())
                {
                    await SendTextMessageAsync(recipientId, "❌ عذراً، لا توجد منتجات في هذه الفئة حالياً.");
                    return;
                }

                // ✅ تحويل المنتجات لكروت
                var elements = filtered.Select(p =>
                {
                    var imageUrl = "https://via.placeholder.com/400x300.png?text=No+Image";

                    // التعامل مع الصور
                    if (p.ContainsKey("Image") && p["Image"] != null)
                    {
                        if (p["Image"] is string[] imgs && imgs.Length > 0)
                            imageUrl = imgs[0];
                        else if (p["Image"] is JsonElement imgEl && imgEl.ValueKind == JsonValueKind.Array && imgEl.GetArrayLength() > 0)
                            imageUrl = imgEl[0].GetProperty("url").GetString();
                    }

                    var price = p.ContainsKey("PricePerMeter") ? p["PricePerMeter"]?.ToString() : "غير محدد";
                    var name = p.ContainsKey("Name") ? p["Name"]?.ToString() : "منتج";

                    return new
                    {
                        title = TruncateText(name, 80),
                        subtitle = $"💰 السعر: {price} جنيه/متر",
                        image_url = imageUrl,
                        buttons = new[]
                        {
                            new { type = "postback", title = "📄 التفاصيل", payload = $"DETAIL_{p["Id"]}" }
                        }
                    };
                }).ToList();

                // ✅ Messenger يسمح بـ 10 كروت في الرسالة
                var chunks = elements.Chunk(10).ToList();

                foreach (var chunk in chunks)
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
                                    elements = chunk
                                }
                            }
                        }
                    };

                    await SendRequestAsync(payload);
                    await Task.Delay(800); // تأخير بين المجموعات
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ShowProductsAsync: {ex.Message}");
                await SendTextMessageAsync(recipientId, "❌ حدث خطأ أثناء تحميل المنتجات");
            }
        }

        // ✅ معالجة postback
        private async Task HandlePostbackAsync(string senderId, string payload)
        {
            try
            {
                Console.WriteLine($"📥 Received postback: {payload}");

                if (payload.StartsWith("MAIN_"))
                {
                    var mainId = payload.Replace("MAIN_", "");
                    await ShowSubCategoriesAsync(senderId, mainId);
                }
                else if (payload.StartsWith("SUB_"))
                {
                    var subId = payload.Replace("SUB_", "");
                    await ShowProductsAsync(senderId, subId, isMainCategory: false);
                }
                else if (payload.StartsWith("DETAIL_"))
                {
                    var productId = payload.Replace("DETAIL_", "");
                    await ShowProductDetailsAsync(senderId, productId);
                }
                else if (payload == "GET_STARTED")
                {
                    await SendTextMessageAsync(senderId, "👋 أهلاً بيك في متجر الأقمشة! اختار الفئة اللي تناسبك:");
                    await ShowMainCategoriesAsync(senderId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in HandlePostbackAsync: {ex.Message}");
            }
        }

        // ✅ عرض تفاصيل منتج
        private async Task ShowProductDetailsAsync(string senderId, string productId)
        {
            try
            {
                var products = await _airtable.GetProductsAsync();
                var product = products.FirstOrDefault(p => p["Id"]?.ToString() == productId);

                if (product == null)
                {
                    await SendTextMessageAsync(senderId, "❌ عذراً، لم نجد هذا المنتج.");
                    return;
                }

                var name = product.ContainsKey("Name") ? product["Name"]?.ToString() : "منتج";
                var price = product.ContainsKey("PricePerMeter") ? product["PricePerMeter"]?.ToString() : "غير محدد";
                var description = product.ContainsKey("Description") ? product["Description"]?.ToString() : "لا يوجد وصف";

                var details = $"📦 *{name}*\n\n💰 السعر: {price} جنيه/متر\n\n📝 الوصف:\n{description}";

                await SendTextMessageAsync(senderId, details);

                // إرسال زر للعودة للقائمة الرئيسية
                var payload = new
                {
                    recipient = new { id = senderId },
                    message = new
                    {
                        attachment = new
                        {
                            type = "template",
                            payload = new
                            {
                                template_type = "button",
                                text = "هل تريد الاستمرار؟",
                                buttons = new[]
                                {
                                    new { type = "postback", title = "🏠 القائمة الرئيسية", payload = "GET_STARTED" }
                                }
                            }
                        }
                    }
                };

                await SendRequestAsync(payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in ShowProductDetailsAsync: {ex.Message}");
            }
        }

        // ✅ إرسال الطلب
        private async Task SendRequestAsync(object payload)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                Console.WriteLine($"📤 Sending to Messenger: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = $"https://graph.facebook.com/v20.0/me/messages?access_token={_pageAccessToken}";

                var response = await httpClient.PostAsync(url, content);
                var respText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Messenger API Error: {respText}");
                }
                else
                {
                    Console.WriteLine($"✅ Message sent successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in SendRequestAsync: {ex.Message}");
            }
        }

        // ✅ اختصار النص للحد المسموح به في Messenger
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "غير محدد";

            if (text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}