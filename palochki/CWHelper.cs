﻿using System;
using System.Collections.Generic;
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
        private bool _battleLock;
        private bool _afterBattleLock;
        private byte _arenasPlayed;
        private byte _skipHour;
        private bool _disabled;
        private bool _arenasDisabled;
        private bool _stamaDisabled;
        private bool _autoGdefDisabled;
        private bool _potionForChampDisabled;
        private readonly string _pinTrigger;
        private string _pin;
        private List<int> _fightTriggerIds;
        private int _lastBadRequestId;
        public User User { get; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; set; }
        public DialogHandler SavesChat { get; set; }
        public ChannelHandler GuildChat { get; set; }
        public ChannelHandler CorovansLogChat { get; set; }
        private string _lastFoundFight;
        private bool _disabledRat;
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
            _disabled = false;
            _arenasDisabled = !User.EnableAllCW;
            _stamaDisabled = !User.EnableAllCW;
            _autoGdefDisabled = !User.EnableAllCW;
            _disabledRat = false;
            _potionForChampDisabled = !User.EnableAllCW;
            _pinTrigger = user.Username + " пин";
            _pin = "";
            _lastBadRequestId = 0;
            _fightTriggerIds = new List<int>();
        }

        public async Task InitHelper()
        {
            await Client.ConnectAsync();
            if (!Client.IsUserAuthorized())
            {
                Console.WriteLine($"\nПользователь {User.Username} не авторизован на этом устройстве\n");
                await ExtraUtilities.AuthClient(Client);
            }

            var botIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Constants.BotName);
            var botIds = botIdsQuery.Split('\t');
            CwBot = new DialogHandler(Client, Convert.ToInt32(botIds[0]), Convert.ToInt64(botIds[1]));

            var guildChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.GuildChatName);
            var guildChatIds = guildChatIdsQuery.Split('\t');
            GuildChat = new ChannelHandler(Client, Convert.ToInt32(guildChatIds[0]), Convert.ToInt64(guildChatIds[1]));

            var savesChatIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Client.Session.TLUser.FirstName);
            var savesChatIds = savesChatIdsQuery.Split('\t');
            SavesChat = new DialogHandler(Client, Convert.ToInt32(savesChatIds[0]), Convert.ToInt64(savesChatIds[1]));

            await SavesChat.SendMessage("Бот перезапущен");

            if (User.ResultsChatName != Constants.AbsendResultsChat)
            {
                var resChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.ResultsChatName);
                var resChatIds = resChatIdsQuery.Split('\t');
                CorovansLogChat = new ChannelHandler(Client, Convert.ToInt32(resChatIds[0]), Convert.ToInt64(resChatIds[1]));
            }

            Console.WriteLine(
                $"\nПользователь {User.Username} подключен\nЧат ги:{User.GuildChatName}\nТриггер на мобов:{User.MobsTrigger}\nКанал для реппортов караванов:{User.ResultsChatName}");
        }

        public async Task PerformStandardRoutine()
        {
            await CheckControls();
            if (_disabled || User.Username == "алух" && _disabledRat)
                return;
            var lastBotMsg = await CwBot.GetLastMessage();
            var last3BotMsgs = await CwBot.GetLastMessages(3);
            var msgsToCheck = await GuildChat.GetLastMessages(10);

            if (msgsToCheck.Any(msgToCheck =>
                string.Compare(msgToCheck?.Message, User.MobsTrigger, StringComparison.InvariantCultureIgnoreCase) ==
                0 && !_fightTriggerIds.Contains(msgToCheck.Id)))
            {
                var msgToCheck = msgsToCheck.First(message =>
                    string.Compare(message?.Message, User.MobsTrigger,
                        StringComparison.InvariantCultureIgnoreCase) == 0 && !_fightTriggerIds.Contains(message.Id));
                await HelpWithMobs(msgToCheck);
                _fightTriggerIds.Add(msgToCheck.Id);
            }

            if (msgsToCheck.Any(m => m != null && m.Message.Contains(_pinTrigger)))
                await TrySetPin(msgsToCheck.FirstOrDefault(m => m.Message.Contains(_pinTrigger)));

            await CheckForStaminaAfterBattle();
            await CheckForBattle();
            await ArenasCheck();

            if (lastBotMsg != null)
            {
                if (lastBotMsg.Message.Contains(Constants.Stama))
                    await UseStamina();

                if (lastBotMsg.Message.Contains(Constants.Korovan))
                    await CatchCorovan(lastBotMsg);

                if (lastBotMsg.Message.Contains(Constants.Village))
                    await MessageUtilities.SendMessage(Client, CwBot.Peer, Constants.Village);

                if (last3BotMsgs.Any(x =>
                    x.Message.Contains(Constants.HasMobs) && x.Message != _lastFoundFight &&
                    x.FromId == Constants.CwBotId))
                {
                    var fightMessage = last3BotMsgs.First(x =>
                        x.Message.Contains(Constants.HasMobs) && x.FromId == Constants.CwBotId);
                    _lastFoundFight = fightMessage.Message;
                    await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer,
                        fightMessage.Id);
                }
            }

            Console.WriteLine($"{DateTime.Now}: {User.Username}: цикл проверок завершен");
        }

        private async Task TrySetPin(TLMessage msg)
        {
            if(msg.Id == _lastBadRequestId)
                return;
            var parsed = msg.Message.Split(' ');
            if (parsed.Length != 3)
            {
                await GuildChat.SendMessage($"Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
                _lastBadRequestId = msg.Id;
                return;
            }
            
            var pin = msg.Message.Split(' ')[2];
            
            if (Constants.Castles.Contains(pin))
            {
                await CwBot.SendMessage(Constants.HeroCommand);
                Thread.Sleep(1500);
                var heroReply = await CwBot.GetLastMessage();
                var atkCommandMarkup = heroReply.ReplyMarkup as TLReplyKeyboardMarkup;
                var atkCommand = atkCommandMarkup.Rows[0].Buttons[0] as TLKeyboardButton;
                await CwBot.SendMessage(atkCommand.Text);
                Thread.Sleep(2000);
                var replyToAttack = await CwBot.GetLastMessage();
                if (replyToAttack.Message != "Смелый вояка! Выбирай врага")
                {
                    await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, replyToAttack.Id);
                    _lastBadRequestId = replyToAttack.Id;
                    return;
                }
            }
            else if (!pin.Contains("_atk") && !pin.Contains("_def") && !pin.Contains("🛡Защита"))
            {
                await GuildChat.SendMessage("не распознал пин");
                _lastBadRequestId = msg.Id;
                return;
            }

            await CwBot.SendMessage(pin);
            Thread.Sleep(1500);
            var reply = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, reply.Id);
            _lastBadRequestId = msg.Id;
        }

        private async Task CheckControls()
        {
            var lastMsg = await SavesChat.GetLastMessage();

            switch (lastMsg.Message)
            {
                case "help":
                    await SavesChat.SendMessage(
                        "stop bot = полностью выключить бота\nstart bot = полностью включить бота\nenable arenas = включить автоарены\ndisable arenas = выключить автоарены\nenable stama = включить автослив стамины\ndisable stama = выключить автослив стамины\nenable def = включить автогидеф\ndisable def = выключить автогидеф\nbot status = состояние функций бота\ndisable potions = выключить автозелья на чемпа\nenable potions = включить автозелья на чемпа");
                    break;
                case "bot status":
                    await SavesChat.SendMessage(
                        $"бот = {(_disabled?"выключен":"включен")}\nарены = {(_arenasDisabled?"выключены":"включены")}\nавтослив стамины = {(_stamaDisabled?"выключен":"включен")}\nавтогидеф = {(_autoGdefDisabled?"выключен":"включен")}\nзелья на чемпа = {(_potionForChampDisabled?"выключены":"включены")}");
                    break;
                case "stop bot":
                    await SavesChat.SendMessage("Бот остановлен");
                    _disabled = true;
                    break;
                case "start bot":
                    await SavesChat.SendMessage("Бот запущен");
                    _disabled = false;
                    if (User.Username == "алух")
                        _disabledRat = false;
                    break;
                case "enable arenas":
                    await SavesChat.SendMessage("Автоарены включены");
                    _arenasDisabled = false;
                    break;
                case "disable arenas":
                    await SavesChat.SendMessage("Автоарены выключены");
                    _arenasDisabled = true;
                    break;
                case "enable stama":
                    await SavesChat.SendMessage("Автослив стамины включен");
                    _stamaDisabled = false;
                    break;
                case "enable potions":
                    _potionForChampDisabled = false;
                    break;
                case "disable stama":
                    await SavesChat.SendMessage("Автослив стамины выключен");
                    _stamaDisabled = true;
                    break;
                case "enable def":
                    await SavesChat.SendMessage("Автогдеф включен");
                    _autoGdefDisabled = false;
                    break;
                case "disable def":
                    await SavesChat.SendMessage("Автогдеф выключен");
                    _autoGdefDisabled = true;
                    break;
                case "disable rat":
                    _disabledRat = true;
                    break;
                case "disable potions":
                    _potionForChampDisabled = true;
                    break;
                case "enable all":
                    await SavesChat.SendMessage("Все функции активированы");
                    _autoGdefDisabled = false;
                    _stamaDisabled = false;
                    _arenasDisabled = false;
                    _potionForChampDisabled = false;
                    break;
            }
        }

        private async Task CheckForStaminaAfterBattle()
        {
            if (_stamaDisabled)
                return;
            var afterBattleHours = new[] {1, 9, 17};
            const int afterBattleMinute = 8;
            var time = DateTime.Now;
            if (afterBattleHours.Contains(time.Hour) && time.Minute == afterBattleMinute)
            {
                if (!_afterBattleLock)
                {
                    await CwBot.SendMessage(Constants.HeroCommand);
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (!botReply.Message.Contains(Constants.StaminaNotFull))
                    {
                        await UseStamina();
                        Thread.Sleep(2000);
                    }

                    await CwBot.SendMessage(Constants.GetReportCommand);
                    Thread.Sleep(1000);
                    botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.ReportsHeader))
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, botReply.Id);

                    Console.WriteLine($"{DateTime.Now}: {User.Username}: репорт отправлен");

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
            if(_stamaDisabled)
                return;
            await CwBot.SendMessage(Constants.QuestsCommand);
            Thread.Sleep(1000);
            var botReply = await CwBot.GetLastMessage();
            var buttonNumber = 2;
            if (botReply.Message.Contains(Constants.ForestQuestForRangers) || botReply.Message.Contains(Constants.ForestQuestForRangersN))
                buttonNumber = 0;
            if (botReply.Message.Contains(Constants.SwampQuestForRangers) || botReply.Message.Contains(Constants.SwampQuestForRangersN))
                buttonNumber = 1;
            await CwBot.PressButton(botReply, 0, buttonNumber);
            Console.WriteLine($"{DateTime.Now}: {User.Username}: единица стамины использована(переполнение)");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.Username} юзнул автослив стамы\n");
        }

        private async Task CatchCorovan(TLMessage lastBotMsg)
        {
            await File.AppendAllTextAsync(Constants.CatchesLogFileName,
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

            await File.AppendAllTextAsync(Constants.CatchesLogFileName, $"{DateTime.Now} - задержан\n");
            Console.WriteLine($"{DateTime.Now}: {User.Username}: пойман корован");
        }

        private async Task HelpWithMobs(TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await GuildChat.SendMessage("Нет реплая на моба");
                return;
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            
            if (!replyMsg.Message.Contains(Constants.HasMobs))
            {
                await GuildChat.SendMessage("Нет мобов в реплае");
                return;
            }

            var lastBotMessage = await CwBot.GetLastMessage();
            if (lastBotMessage.Message == Constants.InFight)
            {
                await GuildChat.SendMessage("уже дерусь");
                return;
            }

            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(1000);
            lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);

            if (replyMsg.Message.Contains("Forbidden Champion") && lastBotMessage.Message.Contains("собрался напасть") && !_potionForChampDisabled)
                await DrinkPotions();

            Console.WriteLine($"{DateTime.Now}: {User.Username}: помог с мобами");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.Username} помог с мобами\n");
        }

        private async Task DrinkPotions()
        {
            await CwBot.SendMessage("/g_withdraw p01 1 p02 1 p03 1 p04 1 p05 1 p06 1");
            Thread.Sleep(1000);
            var botReply = await CwBot.GetLastMessage();
            if (!botReply.Message.Contains("Withdrawing:"))
            {
                await GuildChat.SendMessage("Нет зелий в стоке или прав на их получение");
                return;
            }

            await CwBot.SendMessage(botReply.Message);
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p01");
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p02");
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p03");
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p04");
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p05");
            Thread.Sleep(1500);
            await CwBot.SendMessage("/use_p06");
            Thread.Sleep(1500);
            await GuildChat.SendMessage("выпил зелья");
        }

        private async Task CheckForBattle()
        {
            if(_autoGdefDisabled)
                return;
            var battleHours = new[] {0, 8, 16};
            const int battleMinute = 59;
            var time = DateTime.Now;
            if (battleHours.Contains(time.Hour) && time.Minute == battleMinute)
            {
                if (!_battleLock)
                {
                    await CwBot.SendMessage(Constants.HeroCommand);
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.RestedState))
                    {
                        await CwBot.SendMessage("/g_def");
                        Console.WriteLine($"{DateTime.Now}: {User.Username}: ушел в гидеф");
                        await File.AppendAllTextAsync(Constants.ActionLogFile,
                            $"{DateTime.Now}\n{User.Username} сходил в автогидеф\n");
                    }

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
            if(_arenasDisabled)
                return;
            var time = DateTime.Now;
            if(CheckArenaBlocks(time)) return;

            _skipHour = 25;

            await CwBot.SendMessage(Constants.QuestsCommand);
            Thread.Sleep(1000);
            var botReply = await CwBot.GetLastMessage();
            await CwBot.PressButton(botReply, 1, 1);
            Thread.Sleep(1000);
            botReply = await CwBot.GetLastMessage();
            if (botReply.Message == Constants.BusyState)
            {
                _skipHour = (byte) time.Hour;
                return;
            }
            _arenasPlayed = ExtraUtilities.ParseArenasPlayed(botReply.Message);
            if(_arenasPlayed == 5)
                return;

            await CwBot.SendMessage(Constants.FastFightCommand);
            Thread.Sleep(1000);
            botReply = await CwBot.GetLastMessage();
            if (botReply.Message !=  Constants.SuccessArenaStart)
                _skipHour = (byte)time.Hour;
            if (_arenasPlayed == 4)
                _arenasPlayed = 5;
            Console.WriteLine($"{DateTime.Now}: {User.Username}: ушел на арену");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.Username} сходил на автоарену\n");
            ArenaFightStarted = time;
        }

        private bool CheckArenaBlocks(DateTime time)
        {
            var afterBattleHours = new[] {1, 9, 17};
            var nightHours = new[] {7,8,15,16,23,0};

            if (time.Hour == 13 && time.Minute <= 1)
                _arenasPlayed = 0;
            if(_arenasPlayed == 5)
                return true;
            if(nightHours.Contains(time.Hour) || time.Hour == _skipHour)
                return true;
            if(afterBattleHours.Contains(time.Hour) && time.Minute < 9)
                return true;
            return ArenaFightStarted.AddMinutes(6) > time;
        }
    }
}