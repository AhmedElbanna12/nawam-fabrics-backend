using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text.Json;
using System.Linq;

namespace fabrics.Services
{
    public class TelegramService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _filePath;
        private readonly object _fileLock = new();

        public TelegramService(IConfiguration config)
        {
            var botToken = config["Telegram:BotToken"];
            _botClient = new TelegramBotClient(botToken);
            _filePath = Path.Combine(AppContext.BaseDirectory, "vendors.json");

            // ⚠️ تصحيح: ننشئ الملف فقط إذا لم يكن موجودًا
            if (!File.Exists(_filePath))
            {
                var initialData = new VendorList();
                File.WriteAllText(
                    _filePath,
                    JsonSerializer.Serialize(initialData, new JsonSerializerOptions { WriteIndented = true })
                );
            }
        }

        // تسجيل المستخدم تلقائيًا عند وصول Update من Telegram
        public async Task RegisterUserAsync(Update update)
        {
            if (update.Message is null || update.Message.Chat is null)
                return;

            var chatId = update.Message.Chat.Id;
            var firstName = update.Message.Chat.FirstName ?? "Unknown";

            VendorList data;
            try
            {
                var json = File.ReadAllText(_filePath);
                data = JsonSerializer.Deserialize<VendorList>(json) ?? new VendorList();
            }
            catch
            {
                data = new VendorList();
            }

            if (!data.ChatIds.Contains(chatId))
            {
                data.ChatIds.Add(chatId);

                lock (_fileLock)
                {
                    File.WriteAllText(
                        _filePath,
                        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })
                    );
                }

                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: $"✅ تم تسجيلك يا {firstName} لاستقبال بيانات الحجوزات."
                    );
                    Console.WriteLine($"📦 تم تسجيل {firstName} ({chatId})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل إرسال رسالة الترحيب لـ {chatId}: {ex.Message}");
                }
            }
            else
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"أنت مسجل بالفعل ✅"
                );
            }
        }

        // إرسال رسالة لجميع البائعين المسجلين
        public async Task SendMessageAsync(string message)
        {
            VendorList data;
            try
            {
                var json = File.ReadAllText(_filePath);
                data = JsonSerializer.Deserialize<VendorList>(json) ?? new VendorList();
            }
            catch
            {
                data = new VendorList();
            }

            if (data.ChatIds.Count == 0)
            {
                Console.WriteLine("⚠️ لا يوجد بائعين مسجلين بعد.");
                return;
            }

            foreach (var chatId in data.ChatIds.ToList()) // نستخدم ToList لتجنب مشاكل التعديل أثناء التكرار
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: message
                    );
                    Console.WriteLine($"📨 تم إرسال الرسالة لـ {chatId}");
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("USER_IS_BLOCKED"))
                {
                    Console.WriteLine($"⚠️ المستخدم {chatId} عمل بلوك للبوت. سيتم تجاهل الرسائل مؤقتًا.");
                    // اختيارياً: يمكن إزالة الـ chatId مؤقتًا أو تسجيله في قائمة blocked
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل الإرسال لـ {chatId}: {ex.Message}");
                }
            }
        }

        public async Task SendImagesAsync(List<string> Images)
        {
            VendorList data;
            try
            {
                var json = File.ReadAllText(_filePath);
                data = JsonSerializer.Deserialize<VendorList>(json) ?? new VendorList();
            }
            catch
            {
                data = new VendorList();
            }

            // تحقق من وجود صور للإرسال
            if (data.ChatIds.Count == 0 || Images == null || Images.Count == 0)
            {
                Console.WriteLine("⚠️ لا يوجد بائعين مسجلين أو لا توجد صور.");
                return;
            }

            foreach (var chatId in data.ChatIds.ToList())
            {
                try
                {
                    if (Images.Count > 1)
                    {
                        // 🖼️ أكثر من صورة: إرسال كألبوم باستخدام SendMediaGroupAsync
                        var media = Images
                            .Select(url => new InputMediaPhoto(InputFile.FromUri(url)) as IAlbumInputMedia)
                            .ToArray();

                        if (media.Length > 0 && media[0] is InputMediaPhoto firstPhoto)
                        {
                            firstPhoto.Caption = $"📸 حجز جديد - ({media.Length} صور)";
                        }

                        await _botClient.SendMediaGroup(
                            chatId: chatId,
                            media: media
                        );
                    }
                    else
                    {
                        await _botClient.SendPhoto(
                            chatId: chatId,
                            photo: InputFile.FromUri(Images.First()),
                            caption: "📸 صورة المنتج للحجز الجديد"
                        );
                    }
                    // بعد إرسال الصور، نرسل دائماً قائمة روابط الصور حتى لو نجح الإرسال
                    try
                    {
                        var urlsText = "🔗 روابط الصور:\n" + string.Join("\n", Images);
                        await _botClient.SendMessage(chatId, urlsText);
                    }
                    catch (Exception ex)
                    {
                        // لا نريد أن يفشل المرسل الكلي إذا فشل إرسال روابط فقط
                        Console.WriteLine($"⚠️ فشل إرسال روابط الصور لـ {chatId}: {ex.Message}");
                    }
                }
                catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("USER_IS_BLOCKED"))
                {
                    Console.WriteLine($"⚠️ المستخدم {chatId} عمل بلوك للبوت. سيتم تجاهل الصور مؤقتًا.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل إرسال الصور لـ {chatId}: {ex.Message}");
                    // عند الفشل نحاول إرسال روابط الصور كبديل حتى يتمكن البائع من الاطلاع عليها
                    try
                    {
                        var fallbackText = "⚠️ فشل إرسال صور المنتج. استخدم الروابط التالية لعرض الصور:\n" + string.Join("\n", Images);
                        await _botClient.SendMessage(chatId, fallbackText);
                    }
                    catch (Exception sendEx)
                    {
                        Console.WriteLine($"❌ فشل إرسال روابط الصور كبديل لـ {chatId}: {sendEx.Message}");
                    }
                }
            }
        }







        private class VendorList
        {
            public List<long> ChatIds { get; set; } = new();
        }
    }
}
