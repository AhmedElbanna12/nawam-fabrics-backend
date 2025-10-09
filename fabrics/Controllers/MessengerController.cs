using fabrics.Models;
using fabrics.Services;
using fabrics.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace fabrics.Controllers
{
    [Route("api/messenger/webhook")]
    [ApiController]
    public class MessengerController : ControllerBase
    {
        private readonly MessengerService _messenger;
        private readonly IAirtableService _airtableService;
        private readonly ILogger<MessengerController> _logger;

        public MessengerController(MessengerService messenger, IAirtableService airtableService, ILogger<MessengerController> logger)
        {
            _messenger = messenger;
            _airtableService = airtableService;
            _logger = logger;
        }

        // ✅ التحقق من الـ Webhook
        [HttpGet]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string verifyToken,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            const string VERIFY_TOKEN = "my_messenger_token";

            _logger.LogInformation($"Webhook verification: Mode={mode}, Token={verifyToken}");

            if (mode == "subscribe" && verifyToken == VERIFY_TOKEN)
            {
                _logger.LogInformation("Webhook verified successfully!");
                return Ok(challenge);
            }
            else
            {
                _logger.LogWarning("Webhook verification failed!");
                return Forbid();
            }
        }

        // ✅ استقبال الرسائل من المستخدمين
        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] JsonElement body)
        {
            try
            {
                _logger.LogInformation("📩 Messenger webhook received");

                if (!body.TryGetProperty("entry", out var entries))
                    return BadRequest("Missing 'entry'");

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("messaging", out var messagingArray))
                        continue;

                    foreach (var messaging in messagingArray.EnumerateArray())
                    {
                        var senderId = messaging.GetProperty("sender").GetProperty("id").GetString();

                        // 🟢 إذا كانت رسالة نصية من المستخدم
                        if (messaging.TryGetProperty("message", out var message))
                        {
                            var text = message.TryGetProperty("text", out var textProp) ? textProp.GetString() : "";
                            _logger.LogInformation($"👤 Message from {senderId}: {text}");

                            // إرسال التصنيفات الرئيسية كأزرار
                            await SendMainCategories(senderId);
                        }

                        // 🟡 إذا كانت postback (ضغط على زر)
                        else if (messaging.TryGetProperty("postback", out var postback))
                        {
                            var payload = postback.GetProperty("payload").GetString();
                            _logger.LogInformation($"🟨 Postback from {senderId}: {payload}");

                            await HandlePostback(senderId, payload);
                        }
                    }
                }

                return Ok("EVENT_RECEIVED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Webhook processing error");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ✅ إرسال التصنيفات الرئيسية
        private async Task SendMainCategories(string senderId)
        {
            var mainCategories = await _airtableService.GetMainCategoriesAsync();

            // Check if categories were found
            if (mainCategories == null || !mainCategories.Any())
            {
                await _messenger.SendTextAsync(senderId, "No categories available.");
                return;
            }

            var elements = new List<GenericTemplateElement>();

            // Create one element ("bubble") in the generic template for each main category
            foreach (var category in mainCategories)
            {
                var element = new GenericTemplateElement
                {
                    Title = category.Name,
                    // Add other properties like subtitle or image_url if available
                    Buttons = new List<Button>
            {
                // Each element can have up to 3 buttons.
                // Here, using one button per category to view its sub-categories.
                new Button
                {
                    Type = "postback",
                    Title = "View Subcategories",
                    Payload = $"MAIN_CATEGORY_{category.Id}"
                }
            }
                };
                elements.Add(element);
            }

            // Send the categories as a generic template carousel
            await _messenger.SendGenericTemplateAsync(senderId, elements);
        }
        

        // ✅ معالجة Postback (ضغط الأزرار)
        private async Task HandlePostback(string senderId, string payload)
        {
            try
            {
                if (payload.StartsWith("MAIN_CATEGORY_"))
                {
                    var mainCategoryId = payload.Replace("MAIN_CATEGORY_", "");
                    await SendSubCategories(senderId, mainCategoryId);
                }
                else if (payload.StartsWith("SUB_CATEGORY_"))
                {
                    var parts = payload.Replace("SUB_CATEGORY_", "").Split('_');
                    if (parts.Length == 2)
                    {
                        var mainCategoryId = parts[0];
                        var subCategoryId = parts[1];
                        await SendProducts(senderId, subCategoryId);
                    }
                }
                else if (payload == "BACK_TO_MAIN")
                {
                    await SendMainCategories(senderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling postback: {payload}");
                await _messenger.SendTextAsync(senderId, "❌ حدث خطأ. حاول مرة أخرى.");
            }
        }

        // ✅ إرسال التصنيفات الفرعية
        private async Task SendSubCategories(string senderId, string mainCategoryId)
        {
            try
            {
                var subCategories = await _airtableService.GetSubCategoriesAsync(mainCategoryId);

                var buttons = new List<Button>();

                // إضافة أزرار التصنيفات الفرعية
                foreach (var subCat in subCategories)
                {
                    buttons.Add(new Button
                    {
                        Type = "postback",
                        Title = subCat.Name,
                        Payload = $"SUB_CATEGORY_{mainCategoryId}_{subCat.Id}"
                    });
                }

                // إضافة زر للعودة للتصنيفات الرئيسية
                buttons.Add(new Button
                {
                    Type = "postback",
                    Title = "🔙 العودة للرئيسية",
                    Payload = "BACK_TO_MAIN"
                });

                await _messenger.SendButtonsAsync(senderId, "📂 اختر التصنيف الفرعي:", buttons);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending subcategories for main category {mainCategoryId}");
                await _messenger.SendTextAsync(senderId, "❌ حدث خطأ في جلب التصنيفات الفرعية.");
            }
        }

        // ✅ إرسال المنتجات
        private async Task SendProducts(string senderId, string categoryId)
        {
            try
            {
                var products = await _airtableService.GetProductsByCategoryAsync(categoryId);

                if (products == null || !products.Any())
                {
                    await _messenger.SendTextAsync(senderId, "❌ لا توجد منتجات في هذا التصنيف.");
                    await SendMainCategories(senderId);
                    return;
                }

                // إذا كان هناك منتج واحد فقط، أرسله كنص عادي
                if (products.Count == 1)
                {
                    var product = products.First();
                    var message = $"🛍️ {product.Name}\n" +
                                 $"📝 {product.Description}\n" +
                                 $"💰 السعر: {product.PricePerMeter} جنيه\n" +
                                 $"🏷️ التصنيف: {await GetCategoryName(product.Category?.FirstOrDefault())}";

                    await _messenger.SendTextAsync(senderId, message);
                }
                else
                {
                    // إرسال المنتجات كـ Generic Template (carousel)
                    var elements = products.Select(product => new GenericTemplateElement
                    {
                        Title = product.Name,
                        Subtitle = $"{product.PricePerMeter} جنيه - {product.Description}",
                        ImageUrl = product.Image,
                        Buttons = new List<Button>
                        {
                            new Button { Type = "postback", Title = "📞 طلب المنتج", Payload = $"ORDER_{product.Id}" },
                            new Button { Type = "postback", Title = "🔙 العودة", Payload = "BACK_TO_MAIN" }
                        }
                    }).ToList();

                    await _messenger.SendGenericTemplateAsync(senderId, elements);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending products for category {categoryId}");
                await _messenger.SendTextAsync(senderId, "❌ حدث خطأ في جلب المنتجات.");
            }
        }

        // ✅ دالة مساعدة للحصول على اسم التصنيف
        private async Task<string> GetCategoryName(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return "غير محدد";

            try
            {
                var categories = await _airtableService.GetAllCategoriesAsync();
                var category = categories.FirstOrDefault(c => c.Id == categoryId);
                return category?.Name ?? "غير محدد";
            }
            catch
            {
                return "غير محدد";
            }
        }
    }
}