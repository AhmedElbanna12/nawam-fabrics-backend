using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using fabrics.Services;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly FaqService _faqService;

        public WebhookController(IConfiguration config)
        {
            _config = config;
            _http = new HttpClient();
            _faqService = new FaqService(_config); // ✅ تمرير config
        }

        // ✅ (1) لتأكيد الاتصال من Meta
        [HttpGet]
        public IActionResult Verify([FromQuery(Name = "hub.mode")] string mode,
                                    [FromQuery(Name = "hub.verify_token")] string token,
                                    [FromQuery(Name = "hub.challenge")] string challenge)
        {
            var verifyToken = _config["WhatsApp:VerifyToken"];
            if (mode == "subscribe" && token == verifyToken)
            {
                Console.WriteLine("✅ Webhook verified successfully.");
                return Ok(challenge);
            }
            return Forbid();
        }

        // ✅ (2) استقبال الرسائل من العملاء
        [HttpPost]
        public async Task<IActionResult> Receive([FromBody] JsonElement body)
        {
            Console.WriteLine("📩 Webhook received:");
            Console.WriteLine(body.ToString());

            try
            {
                var entry = body.GetProperty("entry")[0];
                var changes = entry.GetProperty("changes")[0];
                var value = changes.GetProperty("value");

                if (!value.TryGetProperty("messages", out JsonElement messages))
                    return Ok();

                var msg = messages[0];
                var from = msg.GetProperty("from").GetString();
                var text = msg.GetProperty("text").GetProperty("body").GetString();

                Console.WriteLine($"👤 العميل ({from}): {text}");

                // ✏️ حفظ رسالة العميل
                LogMessage("customer", from, text);

                // ✅ استدعاء الخدمة لتحديد الرد المناسب (يدعم الذكاء الصناعي)
                var reply = await _faqService.GetReplyAsync(text);

                // 🔁 إرسال الرد للعميل
                await SendMessageAsync(from, reply);

                // ✏️ حفظ رد البوت
                LogMessage("bot", from, reply);

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                return Ok();
            }
        }

        // ✅ إرسال رسالة عبر WhatsApp Cloud API
        private async Task SendMessageAsync(string to, string message)
        {
            var token = _config["WhatsApp:AccessToken"];
            var phoneId = _config["WhatsApp:PhoneNumberId"];

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                messaging_product = "whatsapp",
                to = to,
                type = "text",
                text = new { body = message }
            };

            var url = $"https://graph.facebook.com/v20.0/{phoneId}/messages";
            var response = await _http.PostAsJsonAsync(url, payload);
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"📤 WhatsApp API response: {result}");
        }

        // ✅ حفظ المحادثة في ملف نصي
        private void LogMessage(string sender, string customerPhone, string message)
        {
            var log = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {sender.ToUpper()} | {customerPhone} | {message}\n";
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "chat_log.txt");
            System.IO.File.AppendAllText(logPath, log);
            Console.WriteLine($"📝 Saved to chat_log.txt → {message}");
        }
    }
}
