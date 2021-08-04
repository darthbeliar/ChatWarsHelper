using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace palochki
{
    internal static class MessageUtilities
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
                try
                {
                    var msgHistory = await client.GetHistoryAsync(peer, limit: 1) as TLMessagesSlice;
                    return msgHistory?.Messages[0] as TLMessage;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public static async Task<List<TLMessage>> GetLastMessages(TelegramClient client, TLAbsInputPeer peer,
            bool isChannel = false,int count = 2)
        {
            if (isChannel)
            {
                var msgHistory = await client.GetHistoryAsync(peer, limit: count) as TLChannelMessages;
                return msgHistory?.Messages.Select(msg => msg as TLMessage).ToList();
            }
            else
            {
                var msgHistory = await client.GetHistoryAsync(peer, limit: count) as TLMessagesSlice;
                return msgHistory?.Messages.Select(msg => msg as TLMessage).ToList();
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

        public static async Task ReplyToMsg(TelegramClient client, TLAbsInputPeer peer, string text, int msgId)
        {
            var randomId = TLSharp.Core.Utils.Helpers.GenerateRandomLong();
            var send = new TLRequestSendMessage
            {
                Peer = peer,
                Message = text,
                ReplyToMsgId = msgId,
                RandomId = randomId
            };
            await client.SendRequestAsync<TLRequestSendMessage>(send);
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

        public static async Task ForwardMessage(TelegramClient client, TLAbsInputPeer fromPeer, TLAbsInputPeer toPeer, int messageId)
        {
            var randomIds = new TLVector<long> {TLSharp.Core.Utils.Helpers.GenerateRandomLong()};
            var forwardRequest = new TLRequestForwardMessages
            {
                FromPeer = fromPeer,
                Id = new TLVector<int>{messageId},
                ToPeer = toPeer,
                RandomId = randomIds
            };

            await client.SendRequestAsync<TLUpdates>(forwardRequest);
        }
    }
}
