using LineDC.Messaging;
using LineDC.Messaging.Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat.Controllers
{
    [Route("line/pushMessage")]
    [ApiController]
    public class LineBotPushMessageController : ControllerBase
    {
        private ILineMessagingClient LineMessagingClient { get; }
        private ILogger Logger { get; }
        private IChatAdminThreadStore Store { get; }

        public LineBotPushMessageController(ILineMessagingClient lineMessagingClient,
            ILogger<LineBotPushMessageController> logger, IChatAdminThreadStore store)
        {
            LineMessagingClient = lineMessagingClient;
            Logger = logger;
            Store = store;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string threadId = data.threadId;
            string userId = data.userId;
            string messageContent = data.request.content;
            string senderDisplayName = data.request.senderDisplayName;

            // ユーザー情報が存在すればLINEへ送信
            if (Store.LineUserIdentityStore.TryGetValue(threadId, out var lineUserId))
            {
                // 絵文字画像URLを生成する
                var emoji = Store.UseConfigStore[userId].Emoji;
                var bytes = new UTF32Encoding(true, false).GetBytes(emoji);
                var result = string.Join(string.Empty, bytes.Select(b => string.Format("{0:x2}", b)));
                var emojiCode = result.Substring(result.Length - 5, 5);
                var emojiUrl = $"https://raw.githubusercontent.com/twitter/twemoji/master/assets/72x72/{emojiCode}.png";

                await LineMessagingClient.PushMessageAsync(lineUserId, new List<ISendMessage>
                {
                    new TextMessage(messageContent, null, new Sender(senderDisplayName, emojiUrl))
                });
            }
            return Ok();
        }
    }
}
