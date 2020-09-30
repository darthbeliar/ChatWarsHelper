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
        private const string InFight = "Ты собрался напасть на врага";
        private const string Village = "/pledge";
        private const string HasMobs = "/fight";
        private const string SuccessArenaStart = "Жажда крови одолела тебя, ты пошел на Арену.";
        private const int CwBotId = 265204902;

        private bool _battleLock;
        private bool _afterBattleLock;
        private byte _arenasPlayed;
        private byte _skipHour;
        public User User { get; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; set; }
        public ChannelHandler GuildChat { get; set; }
        public ChannelHandler CorovansLogChat { get; set; }
        private string _lastFoundFight;
        public DateTime ArenaFightStarted { get; private set; }

        public CwHelper(User user)
        {
            User = user;
            Client = new TelegramClient(user.ApiId, user.ApiHash,null,user.Username);
            _lastFoundFight = "";
            _battleLock = false;
            _afterBattleLock = false;
            _arenasPlayed = 0;
            _skipHour = 25;
            ArenaFightStarted = DateTime.MinValue;
        }

        public async Task InitHelper()
        {
            await Client.ConnectAsync();
            var botIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, "Chat Wars 3");
            var botIds = botIdsQuery.Split('\t');
            CwBot = new DialogHandler(Client, Convert.ToInt32(botIds[0]), Convert.ToInt64(botIds[1]));
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

            if (string.Compare(msgToCheck?.Message, User.MobsTrigger, StringComparison.InvariantCultureIgnoreCase) ==
                0)
            {
                var mob = await HelpWithMobs(msgToCheck);
                if (!string.IsNullOrEmpty(mob))
                    _lastFoundFight = mob;
            }

            await CheckForStaminaAfterBattle();

            Console.WriteLine($"\n{DateTime.Now}");
            if (lastBotMsg != null)
            {
                Console.WriteLine(lastBotMsg.Message.Substring(0, Math.Min(lastBotMsg.Message.Length, 100)));

                await CheckForBattle();
                await ArenasCheck();

                if (lastBotMsg.Message.Contains(Stama))
                    await UseStamina();

                if (lastBotMsg.Message.Contains(Korovan))
                    await CatchCorovan(lastBotMsg);

                if (lastBotMsg.Message.Contains(Village))
                    await MessageUtilities.SendMessage(Client,CwBot.Peer,Village);

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
                        await UseStamina();
                    await CwBot.SendMessage("/report");
                    Thread.Sleep(1000);
                    botReply = await CwBot.GetLastMessage();
                    if (!botReply.Message.Contains("Твои результаты в бою"))
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, botReply.Id);
                    _afterBattleLock = true;
                }
            }
            else
            {
                _afterBattleLock = false;
            }
        }

        private async Task UseStamina()
        {
            await CwBot.SendMessage(@"🗺Квесты");
            Thread.Sleep(1000);
            var botReply = await CwBot.GetLastMessage();
            var buttonNumber = 2;
            if (botReply.Message.Contains("🌲Лес 3мин. 🔥"))
                buttonNumber = 0;
            if (botReply.Message.Contains("🍄Болото 4мин. 🔥"))
                buttonNumber = 1;
            await CwBot.PressButton(botReply, 0, buttonNumber);
        }

        private async Task CatchCorovan(TLMessage lastBotMsg)
        {
            await File.AppendAllTextAsync("logCathes.txt",
                $"\n{DateTime.Now} - Пойман КОРОВАН \n{lastBotMsg.Message}\n");
            var rng = new Random();
            Thread.Sleep(rng.Next(1500, 5000));
            await CwBot.PressButton(lastBotMsg, 0, 0);

            if (CorovansLogChat != null)
            {
                Thread.Sleep(40000);
                var reply = await CwBot.GetLastMessage();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, CorovansLogChat.Peer, reply.Id);
            }

            await File.AppendAllTextAsync("logCathes.txt", $"{DateTime.Now} - задержан\n");
        }

        private async Task<string> HelpWithMobs(TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await GuildChat.SendMessage("Нет реплая на моба");
                return "";
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.Contains("/fight"))
            {
                await GuildChat.SendMessage("Нет мобов в реплае");
                return "";
            }

            var lastBotMessage = await CwBot.GetLastMessage();
            if (lastBotMessage.Message == InFight)
            {
                await GuildChat.SendMessage("уже дерусь");
                return replyMsg.Message;
            }

            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(1000);
            lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
            return replyMsg.Message;
        }

        private async Task CheckForBattle()
        {
            var battleHours = new[] {0, 8, 16};
            const int battleMinute = 59;
            var time = DateTime.Now;
            if (battleHours.Contains(time.Hour) && time.Minute == battleMinute)
            {
                if (!_battleLock)
                {
                    await CwBot.SendMessage("🏅Герой");
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains("🛌Отдых"))
                        await CwBot.SendMessage("/g_def");
                    _battleLock = true;
                }
            }
            else
            {
                _battleLock = false;
            }
        }

        private async Task ArenasCheck()
        {
            var afterBattleHours = new[] {1, 8, 17};
            var nightHours = new[] {7,8,15,16,23,0};

            var time = DateTime.Now;
            if (time.Hour == 13 && time.Minute <= 1)
                _arenasPlayed = 0;
            if(nightHours.Contains(time.Hour) || time.Hour == _skipHour)
                return;
            if(afterBattleHours.Contains(time.Hour) && time.Minute < 9)
                return;
            if(ArenaFightStarted.AddMinutes(6) > time)
                return;

            _skipHour = 25;

            await CwBot.SendMessage(@"🗺Квесты");
            Thread.Sleep(1000);
            var botReply = await CwBot.GetLastMessage();
            await CwBot.PressButton(botReply, 1, 1);
            Thread.Sleep(1000);
            botReply = await CwBot.GetLastMessage();

            _arenasPlayed = ExtraUtilities.ParseArenasPlayed(botReply.Message);
            if(_arenasPlayed == 5)
                return;

            await CwBot.SendMessage("▶️Быстрый бой");
            Thread.Sleep(1000);
            botReply = await CwBot.GetLastMessage();
            if (botReply.Message != SuccessArenaStart)
                _skipHour = (byte)time.Hour;
            ArenaFightStarted = time;
        }
    }
}