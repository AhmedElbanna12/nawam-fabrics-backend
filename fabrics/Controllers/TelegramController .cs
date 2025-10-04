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
            if (update == null)
                return BadRequest();

            // 🟢 استدعاء الخدمة لتسجيل المستخدم
            await _telegramService.RegisterUserAsync(update);

            return Ok();
        }
    }
}

