using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace fabrics.Services
{
    public class FaqService
    {
        private readonly string _openRouterApiKey;
        private readonly HttpClient _httpClient;

        public FaqService(IConfiguration config)
        {
            _openRouterApiKey = config["OpenRouter:ApiKey"];
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);

            // يمكنك إضافة هذه الهيدر لتعريف التطبيق في OpenRouter (اختياري)
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://elnawamfabrics.com");
            _httpClient.DefaultRequestHeaders.Add("X-Title", "Elnawam Fabrics Assistant");
        }

        public async Task<string> GetReplyAsync(string question)
        {
            question = question.Trim().ToLower()
                               .Replace("أ", "ا")
                               .Replace("إ", "ا")
                               .Replace("آ", "ا")
                               .Replace("ة", "ه")
                               .Replace("ى", "ي");

            // ✅ ردود ثابتة (كما كانت في النسخة الأصلية)
            if (question.Contains("نوع قماش ") || question.Contains("مناسب") || question.Contains("كيف اختار") || question.Contains(" انواع الفماش"))
                return "لدينا تشكيلة متكاملة تلبي جميع احتياجاتك على مدار العام:\r\n\r\n· لفصل الصيف: السيلكا القطن الصيفي - خفيف الوزن وبارد على البشرة.\r\n· لفصل الخريف: السيلكا القطن الخريفي - متين يناسب تقلبات الطقس.\r\n· لفصل الشتاء: السيلكا القطن الشتوي - أكثر كثافة يوفر الدفء.\r\n· للدفء والرفاهية: أصوافنا المتميزة (كشمير هندي - إيطالي - جولدن تكس مصري).";

            if (question.Contains("شحن") || question.Contains("المحافظات") || question.Contains("محافظه"))
                return "نعم، نوفر الشحن إلى جميع محافظات مصر حتى باب البيت بفضل الله. متوسط وقت التوصيل يومان لمعظم المحافظات.";

            if (question.Contains("واتساب") || question.Contains("التواصل"))
                return "يمكنك التواصل معنا مباشرة على الواتساب: 01148820088 📱";

            if (question.Contains("السلام") || question.Contains("مرحبا") || question.Contains("هاي"))
                return "👋 أهلاً وسهلاً! أنا بوت خدمة عملاء النوام للأقمشة — كيف أقدر أساعدك اليوم؟ 😊";

            // 🤖 في حالة مفيش رد جاهز — نستخدم الذكاء الصناعي من OpenRouter
            return await GetAiReplyAsync(question);
        }

        private async Task<string> GetAiReplyAsync(string question)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _openRouterApiKey);

                var payload = new
                {
                    model = "google/gemini-2.0-flash-001", // أو أي موديل تاني زي openai/gpt-4o-mini
                    messages = new[]
                    {
                        new {
                            role = "system",
                            content = @"أنت مساعد خدمة عملاء متخصص في محلات 'النوام' للأقمشة والأصواف الفاخرة.
يجب أن تلتزم بالشروط التالية بدقة:
1. رحب بالعميل بلطف.
2. استخدم لغة عربية فصحى.
3. لا تخترع معلومات.
4. أجب على السؤال باحتراف ولطف.
5. استعن فقط بالبيانات الآتية:
- الاسم: محلات النوام للأقمشة والأصواف
- التأسيس: 1972
- العنوان: البحيرة، دمنهور، أمام مدرسة التعاون
- الهاتف: 01148820088
- الموقع: elnawamfabrics.com
- الشحن: لجميع محافظات مصر"
                        },
                        new {
                            role = "user",
                            content = $"سؤال العميل: {question}"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://openrouter.ai/api/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ OpenRouter Error: {error}");
                    return "حدث خطأ أثناء التواصل مع الذكاء الصناعي. حاول مرة أخرى لاحقاً 🙏";
                }

                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"🔍 OpenRouter Raw Response: {result}");

                using var doc = JsonDocument.Parse(result);
                var message = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return message ?? "عذرًا، لم أتلق ردًا من الذكاء الصناعي.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI Error: {ex.Message}");
                return "حدث خطأ أثناء المعالجة. يرجى المحاولة لاحقاً.";
            }
        }
    }
}
