using Telegram.Bot;
using Telegram.Bot.Types;


namespace fabrics.Services
{
    public class TelegramService
    {
        private readonly long _chatId;
        private readonly ITelegramBotClient _botClient;

        public TelegramService(IConfiguration config)
        {
            var botToken = config["Telegram:BotToken"];
            _chatId = long.Parse(config["Telegram:ChatId"]);
            _botClient = new TelegramBotClient(botToken);
        }

        public async Task SendMessageAsync(string message)
        {
            await _botClient.SendMessage(
                chatId: _chatId,
                text: message
            );
        }
    }
}
