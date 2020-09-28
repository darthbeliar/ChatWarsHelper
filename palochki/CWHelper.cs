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

        public User User { get; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; set; }
        public ChannelHandler GuildChat { get; set; }
        public ChannelHandler CorovansLogChat { get; set; }
        private string _lastFoundFight;
        private bool _afterBattleLock;

        public CwHelper(User user)
        {
            User = user;
            _mobsTrigger = user.MobsTrigger;
            Client = new TelegramClient(user.ApiId, user.ApiHash,null,user.Username);
            _lastFoundFight = "";
            _battleLock = false;
            _afterBattleLock = false;
        }

        public async Task InitHelper()
        {
            await Client.ConnectAsync();
            var botIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, "Chat Wars 3");
            var botIds = botIdsQuery.Split('\t');
            CwBot = new DialogHandler(Client, CwBotId, Convert.ToInt64(botIds[1]));
            var guildChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.GuildChatName);
            var guildChatIds = guildChatIdsQuery.Split('\t');
            GuildChat = new ChannelHandler(Client, Convert.ToInt32(guildChatIds[0]), Convert.ToInt64(guildChatIds[1]));
            if (User.ResultsChatName != "none")
            {
                var resChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.GuildChatName);
                var resChatIds = resChatIdsQuery.Split('\t');
                CorovansLogChat = new ChannelHandler(Client, Convert.ToInt32(resChatIds[0]), Convert.ToInt64(resChatIds[1]));
            }
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