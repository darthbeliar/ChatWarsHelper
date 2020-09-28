using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeleSharp.TL;
using TLSharp.Core;

namespace palochki
{
    internal class CwHelper
    {
        private const string Korovan = "пытается ограбить";
        private const string Stama = "Выносливость восстановлена: ты полон сил";
        private readonly string _mobsTrigger;
        private const string InFight = "Ты собрался напасть на врага";
        private const string Village = "/pledge";
        private const string HasMobs = "/fight";
        private const int CwBotId = 265204902;
        private bool _battleLock;

        public string UserName { get; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; }
        public ChannelHandler GuildChat { get; }
        public ChannelHandler CorovansLogChat { get; }
        private string _lastFoundFight;
        private bool _afterBattleLock;

        public CwHelper(string username, int apiId, string apiHash,long botAHash, int chatId, long chathash, string mobsTrigger,
            int reschatId = 0, long reschathash = 0)
        {
            UserName = username;
            _mobsTrigger = mobsTrigger;
            Client = new TelegramClient(apiId, apiHash,null,username);
            CwBot = new DialogHandler(Client, CwBotId, botAHash);
            GuildChat = new ChannelHandler(Client, chatId, chathash);
            if (reschatId != 0 && reschathash != 0)
                CorovansLogChat = new ChannelHandler(Client, reschatId, reschathash);
            _lastFoundFight = "";
            _battleLock = false;
            _afterBattleLock = false;
        }

        public async Task PerformStandardRoutine()
        {
            var lastBotMsg = await CwBot.GetLastMessage();
            var last3BotMsgs = await CwBot.GetLastMessages(3);
            var msgToCheck = await GuildChat.GetLastMessage();

            if (string.Compare(msgToCheck?.Message, _mobsTrigger, StringComparison.InvariantCultureIgnoreCase) ==
                0)
            {
                var mob = await HelpWithMobs(Client, CwBot, GuildChat, msgToCheck);
                if (!string.IsNullOrEmpty(mob))
                    _lastFoundFight = mob;
            }

            await CheckForStaminaAfterBattle();

            Console.WriteLine($"\n{DateTime.Now}");
            if (lastBotMsg != null)
            {
                Console.WriteLine(lastBotMsg.Message.Substring(0, Math.Min(lastBotMsg.Message.Length, 100)));

                await CheckForBattle(CwBot);

                if (lastBotMsg.Message.Contains(Stama))
                    await UseStamina(CwBot);

                if (lastBotMsg.Message.Contains(Korovan))
                    await CatchCorovan(Client, CwBot, lastBotMsg, CorovansLogChat);

                if (lastBotMsg.Message.Contains(Village))
                    await MessageUtilities.SendMessage(Client, CwBot.Peer, Village);

                if (last3BotMsgs.Any(x => x.Message.Contains(HasMobs) && x.Message != _lastFoundFight && x.FromId == CwBotId))
                {
                    var fightMessage = last3BotMsgs.First(x => x.Message.Contains(HasMobs));
                    _lastFoundFight = fightMessage.Message;
                    await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer,
                        fightMessage.Id);
                }
            }
        }

        private async Task CheckForStaminaAfterBattle()
        {
            var afterBattleHours = new[] {1, 8, 17};
            const int afterBattleMinute = 8;
            var time = DateTime.Now;
            if (afterBattleHours.Contains(time.Hour) && time.Minute == afterBattleMinute)
            {
                if (!_afterBattleLock)
                {
                    await CwBot.SendMessage("🏅Герой");
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (!botReply.Message.Contains("⏰"))
                        await UseStamina(CwBot);
                    _afterBattleLock = true;
                }
            }
            else
            {
                _afterBattleLock = false;
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

        private static async Task CatchCorovan(TelegramClient client, DialogHandler bot, TLMessage lastBotMsg,
            ChannelHandler results)
        {
            await File.AppendAllTextAsync("logCathes.txt",
                $"\n{DateTime.Now} - Пойман КОРОВАН \n{lastBotMsg.Message}\n");
            var rng = new Random();
            Thread.Sleep(rng.Next(1500, 5000));
            await bot.PressButton(lastBotMsg, 0, 0);

            if (results != null)
            {
                Thread.Sleep(40000);
                var reply = await bot.GetLastMessage();
                await MessageUtilities.ForwardMessage(client, bot.Peer, results.Peer, reply.Id);
            }

            await File.AppendAllTextAsync("logCathes.txt", $"{DateTime.Now} - задержан\n");
        }

        private static async Task<string> HelpWithMobs(TelegramClient client, DialogHandler bot, ChannelHandler chat,
            TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await chat.SendMessage("Нет реплая на моба");
                return "";
            }

            var replyMsg = await chat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.Contains("/fight"))
            {
                await chat.SendMessage("Нет мобов в реплае");
                return "";
            }

            var lastBotMessage = await bot.GetLastMessage();
            if (lastBotMessage.Message == InFight)
            {
                await chat.SendMessage("уже дерусь");
                return replyMsg.Message;
            }

            await bot.SendMessage(replyMsg.Message);
            Thread.Sleep(1000);
            lastBotMessage = await bot.GetLastMessage();
            await MessageUtilities.ForwardMessage(client, bot.Peer, chat.Peer, lastBotMessage.Id);
            return replyMsg.Message;
        }

        private async Task CheckForBattle(DialogHandler bot)
        {
            var battleHours = new[] {0, 8, 16};
            const int battleMinute = 59;
            var time = DateTime.Now;
            if (battleHours.Contains(time.Hour) && time.Minute == battleMinute)
            {
                if (!_battleLock)
                {
                    await bot.SendMessage("🏅Герой");
                    Thread.Sleep(2000);
                    var botReply = await bot.GetLastMessage();
                    if (botReply.Message.Contains("🛌Отдых"))
                        await bot.SendMessage("/g_def");
                    _battleLock = true;
                }
            }
            else
            {
                _battleLock = false;
            }
        }
    }
}