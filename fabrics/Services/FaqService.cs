using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace fabrics.Services
{
    public class FaqService
    {
        private readonly string _huggingFaceApiKey;
        private readonly HttpClient _httpClient;

        public FaqService(IConfiguration config)
        {
            _huggingFaceApiKey = config["HuggingFace:ApiKey"];
            _httpClient = new HttpClient();
        }

        public async Task<string> GetReplyAsync(string question)
        {
            question = question.Trim().ToLower();

            // ✅ الردود الشائعة
            if (question.Contains("سعر") || question.Contains("الاسعار"))
                return "💰 أسعارنا تبدأ من 100 جنيه للمتر حسب نوع القماش.";

            if (question.Contains("مواعيد") || question.Contains("العمل") || question.Contains("فتح"))
                return "🕓 مواعيد العمل من 9 صباحًا إلى 9 مساءً طوال الأسبوع ما عدا الجمعة.";

            if (question.Contains("الفروع") || question.Contains("العنوان") || question.Contains("المكان"))
                return "📍 فروعنا: القاهرة - التجمع الخامس، و6 أكتوبر - مول العرب.";

            if (question.Contains("تواصل") || question.Contains("رقم") || question.Contains("واتساب"))
                return "📞 للتواصل المباشر: 01000000000 أو من خلال نفس رقم الواتساب ده.";

            if (question.Contains("منتجات") || question.Contains("انواع") || question.Contains("القماش"))
                return "🧵 متوفر لدينا: أقمشة صوف، قطن، حرير، ليكرا، ومخمل بألوان متعددة.";

            if (question.Contains("السلام") || question.Contains("مرحبا") || question.Contains("هاي"))
                return "👋 أهلاً وسهلاً! أنا بوت خدمة العملاء، ممكن أساعدك في معرفة الأسعار أو المواعيد أو العنوان؟";

            // 🤖 لو مفيش رد معروف — نستخدم الذكاء الصناعي
            return await GetAiReplyAsync(question);
        }

        private async Task<string> GetAiReplyAsync(string question)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _huggingFaceApiKey);

                // ✅ نستخدم موديل Falcon 7B للرد بالعربية الفصحى
                var payload = new
                {
                    inputs = $"العميل كتب بالعربية: {question}\nمن فضلك جاوب بالعربية الفصحى فقط وبأسلوب موظف خدمة عملاء محترف."
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://api-inference.huggingface.co/models/tiiuae/falcon-7b-instruct",
                    content);

                var result = await response.Content.ReadAsStringAsync();

                // ✅ نحاول نقرأ النص الناتج
                using var doc = JsonDocument.Parse(result);

                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var text = doc.RootElement[0].GetProperty("generated_text").GetString();
                    return text ?? "عذرًا، لم أفهم سؤالك تمامًا. ممكن توضحه أكتر؟ 🤔";
                }
                else
                {
                    return "عذرًا، لم أتلق ردًا من الذكاء الصناعي. حاول تاني لاحقًا 🙏";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI Error: {ex.Message}");
                return "حدث خطأ أثناء معالجة سؤالك. من فضلك حاول مرة أخرى لاحقًا 🙏";
            }
        }
    }
}
