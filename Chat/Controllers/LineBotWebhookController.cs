using LineDC.Messaging.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Chat.Controllers
{
    [Route("line/webhook")]
    [ApiController]
    public class LineBotWebhookController : ControllerBase
    {
        private IWebhookApplication App { get; }
        private ILogger Logger { get; }

        public LineBotWebhookController(IWebhookApplication app, ILogger<LineBotWebhookController> logger)
        {
            App = app;
            Logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var xLineSignature = Request.Headers["x-line-signature"];
            try
            {
                Logger?.LogTrace($"RequestBody: {body}");
                await App.RunAsync(xLineSignature, body);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex.Message);
            }
            return Ok();
        }
    }
}
