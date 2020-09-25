using System;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace palochki
{
    internal static class ExtraUtilities
    {
        
        private static async Task AuthClient(TelegramClient client,string num)
        {
            var hash = await client.SendCodeRequestAsync(num);
            var code = Console.ReadLine(); //вводишь код, который пришел в телегу
            await client.MakeAuthAsync(num, hash, code);
        }

        private static async Task GetIdsByName(TelegramClient client, string name)
        {
            var chats = await client.GetUserDialogsAsync() as TLDialogsSlice;
            if (chats?.Chats != null)
                foreach (var tlAbsChat in chats.Chats)
                {
                    var channel = tlAbsChat as TLChannel;
                    if (channel == null || channel.Title != name) continue;
                    var id = channel.Id;
                    var hash = channel.AccessHash;
                    Console.WriteLine($"ID = {id}\nAccessHash = {hash}");
                }
        }
    }
}
