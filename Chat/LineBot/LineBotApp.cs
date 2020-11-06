using Azure.Communication;
using Azure.Communication.Chat;
using Azure.Communication.Identity;
using LineDC.Messaging;
using LineDC.Messaging.Webhooks;
using LineDC.Messaging.Webhooks.Events;
using LineDC.Messaging.Webhooks.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Chat.LineBot
{
    public class LineBotApp : WebhookApplication
    {
        private IUserTokenManager UserTokenManager { get; }
        private IChatAdminThreadStore Store { get; }
        private string ChatGatewayUrl { get; }
        private string ResourceConnectionString { get; }
        private ILogger Logger { get; }

        public LineBotApp(ILineMessagingClient client, LineBotSettings settings, ILogger<LineBotApp> logger,
            IChatAdminThreadStore store, IUserTokenManager userTokenManager, IConfiguration chatConfiguration)
            : base(client, settings.ChannelSecret)
        {
            Store = store;
            UserTokenManager = userTokenManager;
            ChatGatewayUrl = Utils.ExtractApiChatGatewayUrl(chatConfiguration["ResourceConnectionString"]);
            ResourceConnectionString = chatConfiguration["ResourceConnectionString"];
            Logger = logger;
        }

        protected override async Task OnFollowAsync(FollowEvent ev)
        {
            Logger?.LogInformation("OnFollowAsync");

            // 参加済みでなければ友だち追加時に新規スレッドを作成
            if (Store.LineUserIdentityStore.ContainsValue(ev.Source.UserId))
            {
                return;
            }

            var (userMri, token, expiresIn) = await UserTokenManager.GenerateTokenAsync(ResourceConnectionString);
            var moderator = new ContosoChatTokenModel
            {
                identity = userMri,
                token = token,
                expiresIn = expiresIn
            };

            var userCredential = new CommunicationUserCredential(moderator.token);
            var chatClient = new ChatClient(new Uri(ChatGatewayUrl), userCredential);

            // プロフィール取得
            var profile = await Client.GetUserProfileAsync(ev.Source.UserId);

            // LINEの表示名でユーザーを作成
            var chatThreadMember = new ChatThreadMember(new CommunicationUser(moderator.identity))
            {
                DisplayName = profile.DisplayName
            };

            var chatThreadClient = await chatClient.CreateChatThreadAsync(
                topic: $"{profile.DisplayName}さん", members: new[] { chatThreadMember });
            Store.Store.Add(chatThreadClient.Id, moderator);

            Logger.LogInformation($"Thread ID: {chatThreadClient.Id}");
            Logger.LogInformation($"Moderator ID: {moderator.identity}");

            // スレッドIDとLINEユーザーIDのペアを保存
            Store.LineUserIdentityStore.Add(chatThreadClient.Id, ev.Source.UserId);

            Store.UseConfigStore[moderator.identity] = new ContosoUserConfigModel { Emoji = " 👑 " };
        }

        protected override async Task OnMessageAsync(MessageEvent ev)
        {
            switch (ev.Message)
            {
                case TextEventMessage textMessage:
                    // 参加しているスレッドにメッセージを送信
                    if (Store.LineUserIdentityStore.Any(pair => pair.Value == ev.Source.UserId))
                    {
                        // スレッド・ユーザー情報を取得
                        var threadId = Store.LineUserIdentityStore.First(pair => pair.Value == ev.Source.UserId).Key;
                        var moderator = Store.Store[threadId];

                        // メッセージ送信                        
                        var userCredential = new CommunicationUserCredential(moderator.token);
                        var chatClient = new ChatClient(new Uri(ChatGatewayUrl), userCredential);
                        var chatThread = chatClient.GetChatThread(threadId);
                        var chatThreadClient = chatClient.GetChatThreadClient(threadId);
                        await chatThreadClient.SendMessageAsync(textMessage.Text);
                    }
                    break;

                case MediaEventMessage mediaMessage:
                case FileEventMessage fileMessage:
                case LocationEventMessage locationMessage:
                case StickerEventMessage stickerMessage:
                default:
                    await Client.ReplyMessageAsync(ev.ReplyToken, "そのメッセージ形式は対応していません。");
                    break;
            }
        }
    }
}
