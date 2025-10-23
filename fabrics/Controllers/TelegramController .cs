using fabrics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;

namespace fabrics.Controllers
{
    [Route("api/telegram")]
    [ApiController]
    public class TelegramController : ControllerBase
    {

        private readonly TelegramService _telegramService;

        public TelegramController(TelegramService telegramService)
        {
            _telegramService = telegramService;
        }

        [HttpPost("update")]
        public async Task<IActionResult> ReceiveUpdate([FromBody] Update update)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            try
            {
                    update = System.Text.Json.JsonSerializer.Deserialize<Update>(body);
                if (update == null)
                    return Ok(); // تجاهل أي تحديث فاضي

                await _telegramService.RegisterUserAsync(update);
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Webhook error: {ex.Message}");
                return Ok(); // لازم نرجع OK دايمًا لتلغرام علشان ما يعيدش المحاولة
            }
        }
    }
}

