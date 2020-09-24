using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace palochki
{
    internal static class Program
    {
        private const int ApiId = 438285; //твой апи прилы с сайта https://my.telegram.org/apps
        private const int ApiId2 = 1846219;
        private const string
            ApiHash = "f8e483e0b7cd38437cf5a9064c43f2cb"; //твой hash прилы с сайта https://my.telegram.org/apps
        private const string
            ApiHash2 = "e3fd2b2f71af0a906ac608b2734fe4ab";

        private const long CwBotAHash = 5368294506206266962;
        private const long CwBotAHash2 = 2382078440132580454;

        private const int TeaId = 1367374268;
        private const long TeaAHash = -2353873925669309700;

        private const int TNTId = 1280438334;
        private const long TNTAHash = 8925615842454227854;

        private const int ResultsId = 1389695282;
        private const long ResultsAHash = -6679838127471252035;

        private static async Task Main()
        {
            try
            {
                await CatchCorovans();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await CatchCorovans();
                throw;
            }
        }

        // ReSharper disable once FunctionRecursiveOnAllPaths
        private static async Task CatchCorovans()
        {
            try
            {
                var trun = new CWHelper("трунь",ApiId,ApiHash,CwBotAHash,TeaId,TeaAHash,"трунь мобы",ResultsId,ResultsAHash);
                await trun.Client.ConnectAsync();
                var beliar = new CWHelper("белиар",ApiId2,ApiHash2,CwBotAHash2,TNTId,TNTAHash,"белиар мобы");
                await trun.Client.ConnectAsync();
                await beliar.Client.ConnectAsync();

                while (true)
                {
                    await trun.PerformStandardRoutine();
                    await beliar.PerformStandardRoutine();
                    Thread.Sleep(8000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync("ErrorsLog.txt", $"{DateTime.Now}\n{e.Message}\n");
                await CatchCorovans();
                throw;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private static async Task AuthClient(TelegramClient client)
        {
            var mobile = new string("79807123831"); //твой номер, на котором акк телеги
            var hash = await client.SendCodeRequestAsync(mobile);
            var code = Console.ReadLine(); //вводишь код, который пришел в телегу
            await client.MakeAuthAsync(mobile, hash, code);
        }

        // ReSharper disable once UnusedMember.Local
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