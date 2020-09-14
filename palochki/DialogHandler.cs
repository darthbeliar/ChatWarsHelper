using System.Collections.Generic;
using System.Threading.Tasks;
using TeleSharp.TL;
using TLSharp.Core;

namespace palochki
{
    internal class DialogHandler
    {
        public TLInputPeerUser Peer { get; }
        private readonly TelegramClient _client;

        public DialogHandler(TelegramClient client, int id, long hash)
        {
            _client = client;
            Peer = new TLInputPeerUser {UserId = id, AccessHash = hash};
        }

        public async Task<TLMessage> GetLastMessage()
        {
            return await MessageUtilities.GetLastMessage(_client, Peer);
        }

        public async Task<List<TLMessage>> GetLastMessages(int count)
        {
            return await MessageUtilities.GetLastMessages(_client, Peer,false,count);
        }

        public async Task SendMessage(string text)
        {
            await MessageUtilities.SendMessage(_client, Peer, text);
        }

        public async Task PressButton(TLMessage message, int row, int button)
        {
            await MessageUtilities.PressButton(_client, Peer, message, row, button);
        }
    }
}
