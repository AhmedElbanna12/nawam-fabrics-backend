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

            foreach (var chatId in data.ChatIds)
            {
                try
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: message
                    );
                    Console.WriteLine($"📨 تم إرسال الرسالة لـ {chatId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ فشل الإرسال لـ {chatId}: {ex.Message}");
                }
            }
        }

        private class VendorList
        {
            public List<long> ChatIds { get; set; } = new();
        }
    }
}
