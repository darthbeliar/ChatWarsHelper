
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace palochki
{
    static class MessageUtilities
    {
        public static async Task<TLMessage> GetLastMessage(TelegramClient client,TLAbsInputPeer peer, bool isChannel = false)
        {
            if (isChannel)
            {
                var msgHistory = await client.GetHistoryAsync(peer, limit: 1) as TLChannelMessages;
                return msgHistory?.Messages[0] as TLMessage;
            }
            else
            {
                var msgHistory = await client.GetHistoryAsync(peer, limit: 1) as TLMessagesSlice;
                return msgHistory?.Messages[0] as TLMessage;
            }
        }

        public static async Task<TLMessage> GetMessageById(TelegramClient client,TLAbsInputPeer peer,int id,bool isChannel = false)
        {

            if (isChannel)
            {
                var msgHistory = await client.GetHistoryAsync(peer,id + 1, limit: 1) as TLChannelMessages;
                return msgHistory?.Messages[0] as TLMessage;
            }
            else
            {
                var msgHistory = await client.GetHistoryAsync(peer, id + 1, limit: 1) as TLMessagesSlice;
                return msgHistory?.Messages[0] as TLMessage;
            }
        }

        public static async Task SendMessage(TelegramClient client,TLAbsInputPeer peer,string text)
        {
            await client.SendMessageAsync(peer, text);
        }

        public static async Task PressButton(TelegramClient client,TLAbsInputPeer peer,TLMessage message, int row, int button)
        {
            var buttons = message.ReplyMarkup as TLReplyInlineMarkup;
            var myButton = buttons?.Rows[row].Buttons[button] as TLKeyboardButtonCallback;

            var tlRequestGetBotCallbackAnswer = new TLRequestGetBotCallbackAnswer
            {
                Peer = peer,
                Data = myButton?.Data,
                MsgId = message.Id
            };

            await client.SendRequestAsync<TLBotCallbackAnswer>(tlRequestGetBotCallbackAnswer);
        }

        public static async Task ForwardMessage(TelegramClient client, TLAbsInputPeer FromPeer, TLAbsInputPeer ToPeer, int MessageId)
        {
            var forwardRequest = new TLRequestForwardMessages()
            {
                FromPeer = FromPeer,
                Id = new TLVector<int>{MessageId},
                ToPeer = ToPeer
            };

            await client.SendRequestAsync<TLUpdates>(forwardRequest);
        }
    }
}
