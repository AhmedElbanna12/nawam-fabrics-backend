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

            if (!System.IO.File.Exists(_filePath))
            {
                var initialData = new VendorList();
                System.IO.File.WriteAllText(
                    _filePath,
                    JsonSerializer.Serialize(initialData, new JsonSerializerOptions { WriteIndented = true })
                );
            }
        }

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

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"✅ تم تسجيلك يا {firstName} لاستقبال بيانات الحجوزات."
                );
                Console.WriteLine($"📦 تم تسجيل {firstName} ({chatId})");
            }
            else
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"أنت مسجل بالفعل ✅"
                );
            }
        }

        public async Task SendMessageAsync(string message)
        {
            VendorList data;
            try
            {
                var json = File.ReadAllText(_filePath);
                data = JsonSerializer.Deserialize<VendorList>(json);
            }
            catch
            {
                data = new VendorList();
            }

            if (data == null || data.ChatIds.Count == 0)
            {
                Console.WriteLine("⚠️ لا يوجد بائعين مسجلين بعد.");
                return;
            }

            foreach (var chatId in data.ChatIds)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: message
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل الإرسال لـ {chatId}: {ex.Message}");
                }
            }
        }

        public async Task SendImagesAsync(List<string> selectedImages)
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
            if (data.ChatIds.Count == 0 || selectedImages == null || selectedImages.Count == 0)
            {
                Console.WriteLine("⚠️ لا يوجد بائعين مسجلين أو لا توجد صور.");
                return;
            }

            foreach (var chatId in data.ChatIds)
            {
                try
                {
                    if (selectedImages.Count > 1)
                    {
                        // 🖼️ أكثر من صورة: إرسال كألبوم باستخدام SendMediaGroupAsync
                        var media = selectedImages
                            // نستخدم InputFile.FromUri لإرسال رابط الصورة مباشرة
                            .Select(url => new InputMediaPhoto(InputFile.FromUri(url)) as IAlbumInputMedia)
                            .ToArray();

                        // ✅ التصحيح: التحويل إلى InputMediaPhoto للوصول إلى خاصية Caption
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
                        // 🖼️ صورة واحدة: إرسال باستخدام SendPhotoAsync
                        await _botClient.SendPhoto(
                            chatId: chatId,
                            // نستخدم InputFile.FromUri لإرسال رابط الصورة مباشرة
                            photo: InputFile.FromUri(selectedImages.First()),
                            caption: "📸 صورة المنتج للحجز الجديد"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل إرسال الصور لـ {chatId}: {ex.Message}");
                    // رسالة تنبيه للبائع عند فشل إرسال الصور
                    await _botClient.SendMessage(chatId, "⚠️ فشل إرسال صور المنتج. قد تكون الروابط غير صالحة أو غير قابلة للوصول.");
                }
            }
        }

        private class VendorList
        {
            public List<long> ChatIds { get; set; } = new();
        }
    }
}
