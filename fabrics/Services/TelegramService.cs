using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text.Json;

namespace fabrics.Services
{
    public class TelegramService
    {
        private readonly TelegramBotClient _botClient;
        private readonly string _filePath;
        private readonly object _fileLock = new();

        public TelegramService(IConfiguration config)
        {
            var botToken = config["Telegram:BotToken"];
            _botClient = new TelegramBotClient(botToken);
            _filePath = Path.Combine(AppContext.BaseDirectory, "vendors.json");

            if (!File.Exists(_filePath))
            {
                var initialData = new VendorList();
                File.WriteAllText(
                    _filePath,
                    JsonSerializer.Serialize(initialData, new JsonSerializerOptions { WriteIndented = true })
                );
            }
        }

        public async Task RegisterUserAsync(Update update)
        {
            if (update.Message == null) return;

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

                await _botClient.SendMessage(chatId, $"✅ تم تسجيلك يا {firstName} لاستقبال بيانات الحجوزات.");
                Console.WriteLine($"📦 تم تسجيل {firstName} ({chatId})");
            }
            else
            {
                await _botClient.SendMessage(chatId, $"أنت مسجل بالفعل ✅");
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
                    await _botClient.SendMessage(chatId, message);
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
