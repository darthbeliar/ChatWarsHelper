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
            catch (CloudPasswordNeededException ex)
            {
                Console.WriteLine("\nВведите облачный пароль\n");
                var password_str = Console.ReadLine();
                var password = await client.GetPasswordSetting();
                await client.MakeAuthWithPasswordAsync(password, password_str);
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
            foreach (var tlAbsChat in chats2.Chats)
            {
                var channel = tlAbsChat as TLChannel;
                if (channel == null || channel.Title != name) continue;
                var id = channel.Id;
                var hash = channel.AccessHash;
                return $"{id}\t{hash}";
            }
            return null;
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
            foreach (var tlAbsUser in chats2.Users)
            {
                var user = tlAbsUser as TLUser;
                if (user == null || user.FirstName != name) continue;
                var id = user.Id;
                var hash = user.AccessHash;
                return $"{id}\t{hash}";
            }
            return null;
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
