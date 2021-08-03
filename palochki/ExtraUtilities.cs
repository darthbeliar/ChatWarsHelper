using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using TLSharp.Core.Exceptions;

namespace palochki
{
    internal static class ExtraUtilities
    {
        public static async Task AuthClient(TelegramClient client)
        {
            Console.WriteLine("\nВведите номер\n");
            var num = Console.ReadLine();
            var hash = await client.SendCodeRequestAsync(num);
            Console.WriteLine("\nВведите код из телеги\n");
            var code = Console.ReadLine(); //вводишь код, который пришел в телегу
            try
            {
                await client.MakeAuthAsync(num, hash, code);
            }
            catch (CloudPasswordNeededException)
            {
                Console.WriteLine("\nВведите облачный пароль\n");
                var passwordStr = Console.ReadLine();
                var password = await client.GetPasswordSetting();
                await client.MakeAuthWithPasswordAsync(password, passwordStr);
            }
        }

        public static async Task<string> GetChannelIdsByName(TelegramClient client, string name)
        {
            var chats = await client.GetUserDialogsAsync() as TLDialogsSlice;
            if (chats?.Chats != null)
                foreach (var tlAbsChat in chats.Chats)
                {
                    var channel = tlAbsChat as TLChannel;
                    if (channel == null || channel.Title != name) continue;
                    var id = channel.Id;
                    var hash = channel.AccessHash;
                    return $"{id}\t{hash}";
                }

            var chats2 = (TLDialogs) await client.GetUserDialogsAsync();
            return (from tlAbsChat in chats2.Chats
                select tlAbsChat as TLChannel
                into channel
                where channel != null && channel.Title == name
                let id = channel.Id
                let hash = channel.AccessHash
                select $"{id}\t{hash}").FirstOrDefault();
        }

        internal static async Task<string> GetChannelNameById(TelegramClient client, int? guildChatId)
        {
            var chats = await client.GetUserDialogsAsync() as TLDialogsSlice;
            if (chats?.Chats != null)
                foreach (var tlAbsChat in chats.Chats)
                {
                    var channel = tlAbsChat as TLChannel;
                    if (channel == null || channel.Id != guildChatId) continue;
                    return channel.Title;
                }

            var chats2 = (TLDialogs) await client.GetUserDialogsAsync();
            return (from tlAbsChat in chats2.Chats
                select tlAbsChat as TLChannel
                into channel
                where channel != null && channel.Id == guildChatId
                select channel.Title).FirstOrDefault();
        }

        public static async Task<string> GetBotIdsByName(TelegramClient client, string name)
        {
            var chats = await client.GetUserDialogsAsync() as TLDialogsSlice;
            if (chats?.Users != null)
                foreach (var tlAbsUser in chats.Users)
                {
                    var user = tlAbsUser as TLUser;
                    if (user == null || user.FirstName != name) continue;
                    var id = user.Id;
                    var hash = user.AccessHash;
                    return $"{id}\t{hash}";
                }

            var chats2 = (TLDialogs) await client.GetUserDialogsAsync();
            return (from tlAbsUser in chats2.Users
                select tlAbsUser as TLUser
                into user
                where user != null && user.FirstName == name
                let id = user.Id
                let hash = user.AccessHash
                select $"{id}\t{hash}").FirstOrDefault();
        }

        public static async Task<string> GetBotIdsByUser(TelegramClient client, string userName)
        {
            var chats = await client.GetUserDialogsAsync() as TLDialogsSlice;
            if (chats?.Users != null)
                foreach (var tlAbsUser in chats.Users)
                {
                    var user = tlAbsUser as TLUser;
                    if (user == null || user.Username != userName) continue;
                    var id = user.Id;
                    var hash = user.AccessHash;
                    return $"{id}\t{hash}";
                }

            var chats2 = (TLDialogs) await client.GetUserDialogsAsync();
            return (from tlAbsUser in chats2.Users
                select tlAbsUser as TLUser
                into user
                where user != null && user.Username == userName
                let id = user.Id
                let hash = user.AccessHash
                select $"{id}\t{hash}").FirstOrDefault();
        }

        public static byte ParseArenasPlayed(string input)
        {
            input = input.Split("сегодня")[1];
            return byte.Parse(Regex.Match(input,@"\d").Value);
        }
    }
}
