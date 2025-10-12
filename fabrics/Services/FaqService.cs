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


            _httpClient.Timeout = TimeSpan.FromSeconds(60);

        }

        public async Task<string> GetReplyAsync(string question)
        {
            question = question.Trim().ToLower()
                                       .Replace("أ", "ا")
                                       .Replace("إ", "ا")
                                       .Replace("آ", "ا")
                                       .Replace("ة", "ه")
                                       .Replace("ى", "ي"); ;


            // ✅ الردود الشائعة 

            if (question.Contains("نوع قماش ") || question.Contains("مناسب") || question.Contains("كيف اختار") || question.Contains(" انواع الفماش") )
                return "لدينا تشكيلة متكاملة تلبي جميع احتياجاتك على مدار العام:\r\n\r\n· لفصل الصيف: السيلكا القطن الصيفي - خفيف الوزن وبارد على البشرة.\r\n· لفصل الخريف: السيلكا القطن الخريفي - متين يناسب تقلبات الطقس.\r\n· لفصل الشتاء: السيلكا القطن الشتوي - أكثر كثافة يوفر الدفء.\r\n· للدفء والرفاهية: أصوافنا المتميزة (كشمير هندي - إيطالي - جولدن تكس مصري).\r\n";


            if (question.Contains("انواع الصوف")  || question.Contains("الفرق بين انواع الصوف"))
                return "كل نوع من أصوافنا عالم من الرفاهية:\r\n\r\n· صوف كشمير هندي: القطعة الفاخرة - يتميز بنعومة استثنائية ودفء لا يضاهى، وهو الاختيار الأمثل للشتاء والمناسبات الخاصة.\r\n· صوف إيطالي: ذوق رفيع وأناقة أوروبية - معروف بقصاته الممتازة وألوانه العصرية، مثالي للخريف وبدايات الشتاء.\r\n· صوف جولدن تكس مصري: الجودة المصرية بلمسة عالمية - يجمع بين المتانة واللمعان الطبيعي، يناسب جميع الفصول الباردة.";



            if (question.Contains("السيلكا القطن") || question.Contains("للطقس البارد جداً"))
                return "بالتأكيد! السيلكا القطن الشتوي مصمم خصيصاً ليقدم دفئاً مريحاً مع الحفاظ على مظهر السيلكا الأنيق، مما يجعله اختياراً مثالياً للشتاء";


            if (question.Contains("توازن ") || question.Contains("الدفء ") || question.Contains("خفيف ")  || question.Contains("دافي") )
                return "إذا كنت تبحث عن هذا التوازن بالذات، نوصي بـ:\r\n\r\n· صوف الكشمير الهندي للدفء الفائق مع وزن خفيف جداً.\r\n· الصوف الإيطالي للخريف وبدايات الشتاء حيث الجو بارد لكن ليس قارس البرودة.\r\n\r\nنوع القماش الصيف الخريف الشتاء\r\nسيلكا قطن صيفي ✅ مثالي ⚠️ يمكن ارتداؤه ❌ غير مناسب\r\nسيلكا قطن خريفي ❌ ثقيل ✅ مثالي ⚠️ يمكن ارتداؤه\r\nسيلكا قطن شتوي ❌ حار ⚠️ دافئ ✅ مثالي\r\nصوف كشمير هندي ❌ ⚠️ للبرد الخفيف ✅ مثالي للبرد القارس\r\nصوف إيطالي ❌ ✅ مثالي ⚠️ للبرد المعتدل\r\nصوف جولدن تكس مصري ❌ ✅ مثالي ✅ ممتاز";


            if (question.Contains("الخريف") || question.Contains("خريف"))
                return "لمناسبة خريفية، ننصحك باختيار:\r\n\r\n· السيلكا القطن الخريفي لمظهر أنيق مع توافق تام مع الطقس.\r\n· أو الصوف الإيطالي إذا أردت مظهراً كلاسيكياً فاخراً.";


            if (question.Contains("شتاء") || question.Contains("الشتاء"))
                return "الاختيار يعتمد على أولوياتك:\r\n\r\n· إذا كانت الأناقة والمظهر البراق هما priority: فالسيلكا القطن الشتوي هو اختيارك.\r\n· إذا كان الدفء والرفاهية المطلقة هما priority: فصوف الكشمير الهندي لا يضاهى.\r\n";


            if (question.Contains("مساعده") || question.Contains(" اختيار القماش المناسب"))
                return "بكل تأكيد! فريقنا متخصص في استشارات الأقمشة. اتصل بنا على [01148820088] أو راسلنا على الواتساب [01148820088] وسنختار لك معاً القماش المثالي بناءً على الموسم، المناسبة، ومقاسك بالضبط.";



            if (question.Contains("شحن") || question.Contains("المحافظات") || question.Contains("محافظه"))
                return "نعم، نوفر الشحن إلى جميع محافظات مصر حتى باب البيت بفضل الله. متوسط وقت التوصيل يومان لمعظم المحافظات.";


            if (question.Contains("قبل الاستلام") || question.Contains("معاينة"))
                return "للأسف لا يمكن المعاينة قبل الاستلام وذلك لحماية المنتج من التلف أو السرقة، حيث أن شركات الشحن تلغي مسؤوليتها في حالة الموافقة على المعاينة. لكن لا تقلق، نوفر لك ضمان الاستبدال أو الاسترجاع بعد استلام المنتج إذا لم يكن مطابقاً للتوقعات.";


            if (question.Contains("طرق الدفع") || question.Contains("ادفع"))
                return "نوفر عدة خيارات سهلة وآمنة للدفع:\r\n\r\n· 💳 الدفع عند الاستلام (مع رسوم عربون ١٠٪)\r\n· 📱 فودافون كاش\r\n· 📲 انستا باي";


            if (question.Contains("عند الاستلام") || question.Contains("الدفع عند الاستلام"))
                return "عند اختيار \"الدفع عند الاستلام\"، نحجز عربون ١٠٪ من إجمالي قيمة الطلبية قبل الشحن، ثم تقوم بدفع المبلغ المتبقي عند استلام الطلبية.\r\n";


            if (question.Contains("استبدال") || question.Contains("استرجاع"))
                return "نعم، نوفر خدمة الاستبدال والاسترجاع مع ضمان جودة المنتج لضمان رضاك التام عن شرائك.\r\n";


            if (question.Contains(" واتساب") || question.Contains("التواصل") || question.Contains("واتس اب"))
                return "نعم، يمكنك التواصل معنا مباشرة على:\r\n01148820088\r\nفريق خدمة العملاء متاح لمساعدتك في أي استفسار.";


            if (question.Contains("أطمئن على طلبي بعد التوصيل"))
                return "جميع طلباتنا مغلقة بشكل آمن ومحكم. في حال وجود أي استفسار عن المنتج بعد الاستلام، يمكنك التواصل معنا على الواتساب وسنقوم بمساعدتك على الفور.";


            if (question.Contains("السلام") || question.Contains("مرحبا") || question.Contains("هاي"))
                return "👋 أهلاً وسهلاً! أنا بوت خدمة العملاء، ممكن أساعدك في معرفة الأسعار أو المواعيد أو العنوان او المنتجات والاصناف المتاحه ؟";

            // 🤖 لو مفيش رد معروف — نستخدم الذكاء الصناعي
            return await GetAiReplyAsync(question);
        }

        private async Task<string> GetAiReplyAsync(string question)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _huggingFaceApiKey);

                // ✅ FIXED: Use correct API endpoint
                var payload = new
                {
                    inputs = $@"
أنت مساعد خدمة عملاء متخصص في محلات 'النوام' للأقمشة والأصواف الفاخرة. 
يجب أن تلتزم بالشروط التالية بدقة:

🎯 **المهمة**: 
الرد على استفسارات العملاء بالعربية الفصحى فقط وبأسلوب محترف ولطيف.

🏢 **معلومات المحل الأساسية**:
- الاسم: محلات النوام للأقمشة والأصواف
- التأسيس: ١٩٧٢
- التخصص: أقمشة وأصواف رجالية فاخرة

🎁 **المنتجات المتاحة**:
• أقمشة السيلكا المستوردة والمصرية
• الصوف المصري، الإنجليزي، والإيطالي
• الصوف الكشمير الهندي

📍 **معلومات التواصل**:
- العنوان: البحيرة، دمنهور، أمام مدرسة التعاون
- الشحن: لجميع محافظات مصر
- واتساب: 01148820088
- الموقع: Elnawamfabrics.com

📝 **تعليمات الرد**:
1. رحب بالعميل بلطف
2. أجب على السؤال مباشرة وباختصار
3. استخدم لغة عربية فصحى واضحة
4. قدم المعلومات ذات الصلة فقط
5. لا تختلق معلومات غير موجودة في البيانات أعلاه

سؤال العميل: '{question}'

الرد المناسب:"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // ✅ FIXED: Correct API URL
                var response = await _httpClient.PostAsync(
                    "https://api-inference.huggingface.co/models/tiiuae/falcon-7b-instruct",
                    content);

                Console.WriteLine($"🔍 API Response Status: {response.StatusCode}");

                // ✅ ADDED: Handle model loading (503 error)
                if ((int)response.StatusCode == 503)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"🔧 Model is loading: {errorContent}");
                    return "نظام الذكاء الصناعي جاري التحميل. يرجى المحاولة مرة أخرى خلال دقيقتين أو التواصل على 01148820088 📞";
                }

                // ✅ ADDED: Handle other errors
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ API Error {response.StatusCode}: {errorContent}");
                    return $"عذرًا، الخدمة غير متاحة حاليًا (Error: {response.StatusCode}). يرجى التواصل على 01148820088";
                }

                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"🔍 Raw API Response: {result}");

                // ✅ نحاول نقرأ النص الناتج
                try
                {
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
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"❌ JSON Parse Error: {jsonEx.Message}");
                    return "عذرًا، حدث خطأ في معالجة الرد. يرجى التواصل على 01148820088";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AI Error: {ex.Message}");
                Console.WriteLine($"❌ AI Stack Trace: {ex.StackTrace}");
                return "هذا السؤال خارج تخصصنا، للاستفسارات المتخصصة يرجى التواصل على الرقم 01148820088";
            }
        }
    