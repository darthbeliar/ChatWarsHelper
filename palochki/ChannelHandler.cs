using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeleSharp.TL;
using TLSharp.Core;

namespace palochki
{
    internal class ChannelHandler
    {
        public TLInputPeerChannel Peer { get; }
        private readonly TelegramClient _client;

        public ChannelHandler(TelegramClient client, int id, long hash)
        {
            _client = client;
            Peer = new TLInputPeerChannel {ChannelId = id, AccessHash = hash};
        }

        public async Task<TLMessage> GetLastMessage()
        {
            return await MessageUtilities.GetLastMessage(_client, Peer, true) ??
                   (await GetLastMessages(10)).FirstOrDefault(m => m != null);
        }

        public async Task<List<TLMessage>> GetLastMessages(int count)
        {
            return await MessageUtilities.GetLastMessages(_client, Peer,true,count);
        }

        public async Task<TLMessage> GetMessageById(int id)
        {
            return await MessageUtilities.GetMessageById(_client, Peer, id,true);
        }

        public async Task SendMessage(string text)
        {
            await MessageUtilities.SendMessage(_client, Peer, text);
        }
    }
}