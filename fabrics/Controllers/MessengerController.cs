using fabrics.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace fabrics.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessengerController : ControllerBase
    {

        private readonly MessengerService _messengerService;

        public MessengerController(MessengerService messengerService)
        {
            _messengerService = messengerService;
        }

        // ✅ Verification endpoint (GET)
        [HttpGet("webhook")]
        public IActionResult VerifyMessengerWebhook(
     [FromQuery(Name = "hub.mode")] string mode,
     [FromQuery(Name = "hub.verify_token")] string verifyToken,
     [FromQuery(Name = "hub.challenge")] string challenge)
        {
            const string VERIFY_TOKEN = "my_messenger_token"; // لازم تكون نفس اللي في Meta Developer

            if (mode == "subscribe" && verifyToken == VERIFY_TOKEN)
            {
                return Ok(challenge);
            }

            return Forbid();
        }


        // ✅ Receive messages (POST)
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessengerMessage([FromBody] JsonElement body)
        {
            await _messengerService.HandleMessageAsync(body);
            return Ok();
        }
    }
}

