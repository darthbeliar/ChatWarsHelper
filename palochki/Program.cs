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

        private const string
            ApiHash = "f8e483e0b7cd38437cf5a9064c43f2cb"; //твой hash прилы с сайта https://my.telegram.org/apps

        private const int CwBotId = 265204902;
        private const int TeaId = 1367374268;
        private const long CwBotAHash = 5368294506206266962;
        private const long TeaAHash = -2353873925669309700;
        private const string Korovan = "пытается ограбить";
        private const string Stama = "Выносливость восстановлена: ты полон сил";
        private const string MobsTrigger = "трунь мобы";
        private const string InFight = "Ты собрался напасть на врага";
        private const string Village = "/pledge";
        private const string HasMobs = "/fight";

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
                var client = new TelegramClient(ApiId, ApiHash);
                await client.ConnectAsync();
                //await AuthClient(client);

                var chatWarsBot = new DialogHandler(client, CwBotId, CwBotAHash);
                var teaChat = new ChannelHandler(client, TeaId, TeaAHash);
                var lastFoundFight = "";

                while (true)
                {
                    var lastBotMsg = await chatWarsBot.GetLastMessage();

                    Console.WriteLine($"\n{DateTime.Now}");
                    if (lastBotMsg != null)
                    {
                        Console.WriteLine(lastBotMsg.Message.Substring(0, Math.Min(lastBotMsg.Message.Length, 100)));

                        await CheckForBattle(chatWarsBot);

                        if (lastBotMsg.Message.Contains(Stama))
                            await UseStamina(chatWarsBot);

                        if (lastBotMsg.Message.Contains(Korovan))
                            await CatchCorovan(chatWarsBot, lastBotMsg);
                        if (lastBotMsg.Message.Contains(Village))
                            await MessageUtilities.SendMessage(client, chatWarsBot.Peer, Village);
                        if (lastBotMsg.Message.Contains(HasMobs) && lastBotMsg.Message != lastFoundFight)
                        {
                            lastFoundFight = lastBotMsg.Message;
                            await MessageUtilities.ForwardMessage(client, chatWarsBot.Peer, teaChat.Peer, lastBotMsg.Id);
                        }
                    }

                    Thread.Sleep(1000);

                    var msgToCheck = await teaChat.GetLastMessage();

                    if (string.Compare(msgToCheck?.Message, MobsTrigger, StringComparison.InvariantCultureIgnoreCase) ==
                        0)
                        await HelpWithMobs(chatWarsBot, teaChat, msgToCheck);

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

        private static async Task UseStamina(DialogHandler bot)
        {
            await bot.SendMessage(@"🗺Квесты");
            Thread.Sleep(1000);
            var botReply = await bot.GetLastMessage();
            var buttonNumber = 2;
            if (botReply.Message.Contains("🌲Лес 3мин. 🔥"))
                buttonNumber = 0;
            if (botReply.Message.Contains("🍄Болото 4мин. 🔥"))
                buttonNumber = 1;
            await bot.PressButton(botReply, 0, buttonNumber);
        }

        private static async Task CatchCorovan(DialogHandler bot, TLMessage lastBotMsg)
        {
            await File.AppendAllTextAsync("logCathes.txt",
                $"\n{DateTime.Now} - Пойман КОРОВАН \n{lastBotMsg.Message}\n");
            var rng = new Random();
            Thread.Sleep(rng.Next(1500, 5000));
            await bot.PressButton(lastBotMsg, 0, 0);

            await File.AppendAllTextAsync("logCathes.txt", $"{DateTime.Now} - задержан\n");
        }

        private static async Task HelpWithMobs(DialogHandler bot, ChannelHandler chat,
            TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await chat.SendMessage("Нет реплая на моба");
            }
            else
            {
                var replyMsg = await chat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
                if (!replyMsg.Message.Contains("/fight"))
                {
                    await chat.SendMessage("Нет мобов в реплае");
                }
                else
                {
                    var lastBotMessage = await bot.GetLastMessage();
                    if (lastBotMessage.Message == InFight)
                    {
                        await chat.SendMessage("уже дерусь");
                    }
                    else
                    {
                        await bot.SendMessage(replyMsg.Message);
                        Thread.Sleep(1000);
                        lastBotMessage = await bot.GetLastMessage();
                        await chat.SendMessage(lastBotMessage.Message);
                    }
                }
            }
        }

        private static async Task CheckForBattle(DialogHandler bot)
        {
            var battleHours = new[] {0, 8, 16};
            const int battleMinute = 59;
            var time = DateTime.Now;
            if (battleHours.Contains(time.Hour) && time.Minute == battleMinute)
            {
                await bot.SendMessage("🏅Герой");
                Thread.Sleep(2000);
                var botReply = await bot.GetLastMessage();
                if (botReply.Message.Contains("🛌Отдых"))
                    await bot.SendMessage("/g_def");
                Thread.Sleep(60000);
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
    }
}