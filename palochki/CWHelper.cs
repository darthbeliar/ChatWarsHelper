using System;
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
        private bool _morningQuest;
        private readonly string _pinTrigger;
        private string _pin;
        private List<int> _fightTriggerIds;
        private int _lastBadRequestId;
        public User User { get; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; set; }
        public DialogHandler SavesChat { get; set; }
        public DialogHandler OrdersChat { get; set; }
        public ChannelHandler GuildChat { get; set; }
        public ChannelHandler CorovansLogChat { get; set; }
        private string _lastFoundFight;
        private bool _disabledRat;
        public DateTime ArenaFightStarted { get; private set; }
        public List<int> PreBattleCounts = new List<int>(39);
        public List<int> AfterBattleCounts = new List<int>(39);

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
            _morningQuest = false;
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

            if (User.AcceptOrders)
            {
                var orderChatIds = (await ExtraUtilities.GetBotIdsByName(Client, User.OrdersChatName)).Split('\t');
                OrdersChat = new DialogHandler(Client, Convert.ToInt32(orderChatIds[0]), Convert.ToInt64(orderChatIds[1]));
            }

            await SavesChat.SendMessage("Бот перезапущен");

            if (User.ResultsChatName != Constants.AbsendResultsChat)
            {
                var resChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.ResultsChatName);
                var resChatIds = resChatIdsQuery.Split('\t');
                CorovansLogChat = new ChannelHandler(Client, Convert.ToInt32(resChatIds[0]), Convert.ToInt64(resChatIds[1]));
            }

            for (var i = 0; i <= 38; i++)
            {
                AfterBattleCounts.Add(0);
                PreBattleCounts.Add(0);
            }
            /*
            var arenaLog = await File.ReadAllLinesAsync("arenas");
            var today = DateTime.Today;
            var searchString = $"{User.Username}\t{today.Day}.{today.Month}.{today.Year}";
            if (arenaLog.Any(s => s.Contains(searchString)))
                _arenasPlayed = Convert.ToByte(arenaLog.FirstOrDefault(s => s.Contains(searchString))?.Split('\t')[2]);
*/

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
            await MorningQuest();

            if (lastBotMsg != null)
            {
                if (lastBotMsg.Message.Contains(Constants.Stama))
                {
                    const int afterBattleMinute = 8;
                    var time = DateTime.Now;
                    if (Constants.AfterBattleHours.Contains(time.Hour) && time.Minute < afterBattleMinute)
                        return;
                    await UseStamina();
                }

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

            if (User.AcceptOrders)
                await CheckOrders();

            if (User.Username == "шпендаль")
                await CheckBotOrder();

            if (User.Username == "алух")
                await CheckGiveOrder();

            Console.WriteLine($"{DateTime.Now}: {User.Username}: цикл проверок завершен");
        }

        private async Task CheckGiveOrder()
        {
            var msgToCheck = await GuildChat.GetLastMessage();
            if (msgToCheck.Message.ToLower() != "дай криса")
                return;
            if (msgToCheck.ReplyToMsgId == null)
            {
                await GuildChat.SendMessage("нужен реплай");
                return;
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.ToLower().Contains("/g_withdraw"))
            {
                await GuildChat.SendMessage("нет запроса выдачи в реплае");
                return;
            }
            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(2000);
            var lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
        }

        private async Task MorningQuest()
        {
            var time = DateTime.Now;
            if (_morningQuest || _stamaDisabled)
                return;
            if (time.Hour == 8 && time.Minute > 10 && time.Minute < 14)
            {
                await UseStamina();
                _morningQuest = true;
            }
        }

        private async Task CheckBotOrder()
        {
            var msgToCheck = await GuildChat.GetLastMessage();
            if (msgToCheck.Message.ToLower() != "в бота")
                return;
            if (msgToCheck.ReplyToMsgId == null)
            {
                await GuildChat.SendMessage("нужен реплай");
                return;
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if(replyMsg.Message.ToLower().Contains("/g_receive"))
                Thread.Sleep(14000);
            if(replyMsg.Message.ToLower().Contains("//g_deposit"))
                Thread.Sleep(7000);
            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(2000);
            var lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
        }

        private async Task CheckOrders()
        {
            var lastMes = await OrdersChat.GetLastMessage();
            if (lastMes.Message.ToLower().Contains(_pinTrigger))
                await TrySetPin(lastMes,true);
            if (lastMes.Message.ToLower().Contains("выпей рагу"))
                await TryDrinkRage();
            if (lastMes.Message.ToLower().Contains("бери"))
                await TryTakeItems(lastMes);
            if (lastMes.Message.ToLower().Contains("скинь героя"))
                await GetHeroMessage();
        }

        private async Task GetHeroMessage()
        {
            await CwBot.SendMessage(Constants.HeroCommand);
            Thread.Sleep(1500);
            var lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, lastBotMessage.Id); 
        }

        private async Task TryTakeItems(TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await OrdersChat.SendMessage("Нет реплая на сообщение");
                return;
            }

            var replyMsg = await OrdersChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.Contains("/g_receive"))
            {
                await OrdersChat.SendMessage("Нет ссылки на итемы");
                return;
            }

            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(10000);
            var lastBotMessage = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, lastBotMessage.Id);
        }

        private async Task TryDrinkRage()
        {
            await CwBot.SendMessage("/misc rage");
            Thread.Sleep(1500);
            var reply = await CwBot.GetLastMessage();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
            if (Constants.RagePots.Any(p => !reply.Message.Contains(p)))
            {
                await OrdersChat.SendMessage("Какого-то зелья не хватает");
                return;
            }
            var time = DateTime.Now;
            if (Constants.BattleHours.Contains(time.Hour) && time.Minute > 30)
            {
                await CwBot.SendMessage("/use_p01");
                Thread.Sleep(1500);
                reply = await CwBot.GetLastMessage();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
                await CwBot.SendMessage("/use_p02");
                Thread.Sleep(1500);
                reply = await CwBot.GetLastMessage();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
                await CwBot.SendMessage("/use_p03");
                Thread.Sleep(1500);
                reply = await CwBot.GetLastMessage();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
            }
            else
            {
                await OrdersChat.SendMessage("До битвы больше чем полчаса");
            }
        }

        private async Task TrySetPin(TLMessage msg,bool personalOrder = false)
        {
            if(msg.Id == _lastBadRequestId)
                return;
            var parsed = msg.Message.Split(' ');

            if (parsed.Length != 3)
            {
                if (personalOrder)
                    await OrdersChat.SendMessage(
                        "Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
                else 
                    await GuildChat.SendMessage("Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
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
                    if (personalOrder)
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, replyToAttack.Id);
                    else
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, replyToAttack.Id);
                    _lastBadRequestId = replyToAttack.Id;
                    return;
                }
            }
            else if (!pin.Contains("_atk") && !pin.Contains("_def") && !pin.Contains("🛡Защита"))
            {
                if(personalOrder)
                    await OrdersChat.SendMessage("не распознал пин");
                else
                    await GuildChat.SendMessage("не распознал пин");
                _lastBadRequestId = msg.Id;
                return;
            }

            await CwBot.SendMessage(pin);
            Thread.Sleep(1500);
            var reply = await CwBot.GetLastMessage();
            if(personalOrder)
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
            else
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
            const int afterBattleMinute = 8;
            var time = DateTime.Now;
            if (Constants.AfterBattleHours.Contains(time.Hour) && time.Minute == afterBattleMinute)
            {
                if (!_afterBattleLock)
                {
                    await CwBot.SendMessage(Constants.GetReportCommand);
                    Thread.Sleep(1000);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.ReportsHeader))
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, botReply.Id);

                    Console.WriteLine($"{DateTime.Now}: {User.Username}: репорт отправлен");

                    if (User.ResultsChatName != Constants.AbsendResultsChat)
                    {
                        Thread.Sleep(2000);
                        await CwBot.SendMessage("/g_stock_res");
                        Thread.Sleep(2000);
                        botReply = await CwBot.GetLastMessage();
                        if (botReply.Message.Contains("Guild Warehouse"))
                        {
                            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, CorovansLogChat.Peer,
                                botReply.Id);
                            AfterBattleCounts = ParseStock(botReply.Message);
                        }

                        if (AfterBattleCounts.Any(i => i != 0) && PreBattleCounts.Any(j => j != 0))
                        {
                            var msg = "Изменения в стоке\n";
                            var noChanges = true;
                            for (var i = 1; i <= 38; i++)
                            {
                                var change = AfterBattleCounts[i] - PreBattleCounts[i];
                                if (change == 0) continue;
                                noChanges = false;
                                var sign = change > 0 ? "+" : "-";
                                msg += $"{Constants.CwItems[i]} {sign}{change}\n";
                            }

                            if(!noChanges)
                                await CorovansLogChat.SendMessage(msg);
                            for (var i = 0; i <= 38; i++)
                            {
                                AfterBattleCounts[i] = 0;
                                PreBattleCounts[i] = 0;
                            }
                        }
                    }

                    await CwBot.SendMessage(Constants.HeroCommand);
                    Thread.Sleep(2000); 
                    botReply = await CwBot.GetLastMessage();
                    if (!botReply.Message.Contains(Constants.StaminaNotFull))
                    {
                        await UseStamina();
                        Thread.Sleep(2000);
                    }

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

            await CwBot.SendMessage(Constants.HeroCommand);
            Thread.Sleep(1500);
            lastBotMessage = await CwBot.GetLastMessage();
            var hp = lastBotMessage.Message.Split("❤️Здоровье: ")[1].Split('/')[0];
            if (Convert.ToInt32(hp) > 900)
            {
                await CwBot.SendMessage(replyMsg.Message);
                Thread.Sleep(1000);
                lastBotMessage = await CwBot.GetLastMessage();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);

                if (replyMsg.Message.Contains("Forbidden Champion") &&
                    lastBotMessage.Message.Contains("собрался напасть") && !_potionForChampDisabled)
                    await DrinkPotions();

                Console.WriteLine($"{DateTime.Now}: {User.Username}: помог с мобами");
                await File.AppendAllTextAsync(Constants.ActionLogFile,
                    $"{DateTime.Now}\n{User.Username} помог с мобами\n");
            }
            else
            {
                if(msgToCheck.FromId == 255464103)
                    await GuildChat.SendMessage("лучше бы /g_q_discard_a10 нажала чем пытаться убить лоухпшного криса");
                else
                {
                    var replys = File.ReadAllLines("replies");
                    var rng = new Random();
                    var i = rng.Next(replys.Length);
                    await GuildChat.SendMessage(replys[i]);
                }
            }
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
            const int battleMinute = 58;
            var time = DateTime.Now;
            if (Constants.BattleHours.Contains(time.Hour) && time.Minute >= battleMinute)
            {
                if (!_battleLock)
                {
                    await CwBot.SendMessage(Constants.HeroCommand);
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.RestedState) || botReply.Message.Contains(Constants.SmithState))
                    {
                        await CwBot.SendMessage("/g_def");
                        Console.WriteLine($"{DateTime.Now}: {User.Username}: ушел в гидеф");
                        await File.AppendAllTextAsync(Constants.ActionLogFile,
                            $"{DateTime.Now}\n{User.Username} сходил в автогидеф\n");
                    }

                    if (User.ResultsChatName != Constants.AbsendResultsChat)
                    {
                        Thread.Sleep(2000);
                        await CwBot.SendMessage("/g_stock_res");
                        Thread.Sleep(2000);
                        botReply = await CwBot.GetLastMessage();
                        if (botReply.Message.Contains("Guild Warehouse"))
                        {
                            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, CorovansLogChat.Peer,
                                botReply.Id);
                            PreBattleCounts = ParseStock(botReply.Message);
                        }
                    }

                    if (User.Username == "белиар")
                    {
                        await CheckRing("u127");
                    }

                    _battleLock = true;
                }
            }
            else
            {
                _battleLock = false;
            }
        }

        private async Task CheckRing(string ringname)
        {
            var atk = false;
            var def = false;
            await CwBot.SendMessage(Constants.HeroCommand);
            Thread.Sleep(2000);
            var reply = await CwBot.GetLastMessage();
            if (reply.Message.Contains("🛡Defending") || reply.Message.Contains("🛡Защита "))
                def = true;
            if (reply.Message.Contains("⚔️Attacking") || reply.Message.Contains("⚔️Атака "))
                atk = true;
            await CwBot.SendMessage("/inv");
            Thread.Sleep(2000);
            reply = await CwBot.GetLastMessage();
            if (reply.Message.Contains($"/off_{ringname}") && def)
                await CwBot.SendMessage($"/off_{ringname}");
            if (reply.Message.Contains($"/on_{ringname}") && atk)
                await CwBot.SendMessage($"/on_{ringname}");
        }

        private static List<int> ParseStock(string botReplyMessage)
        {
            var result = new List<int>(39);
            for (var i = 0; i < 39; i++)
                result.Add(0);
            var strings = botReplyMessage.Split('\n');
            foreach (var s in strings)
            {
                if(s.Contains("Guild Warehouse"))
                    continue;
                var id = int.Parse(s.Split(' ')[0]);
                var count = int.Parse(s.Split("x ")[1]);
                result[id] = count;
            }

            return result;
        }

        private async Task ArenasCheck()
        {
            if(_arenasDisabled)
                return;
            var time = DateTime.Now;
            if(await CheckArenaBlocks(time)) return;

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
            //await UpdateArenasFile(_arenasPlayed,time);

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

            //await UpdateArenasFile(_arenasPlayed,time);
        }

        private async Task UpdateArenasFile(byte arenasPlayed, DateTime time)
        {
            var arenaLog = await File.ReadAllLinesAsync("arenas");
            var searchString = $"{User.Username}\t";
            var index = Array.IndexOf(arenaLog,arenaLog.FirstOrDefault(s => s.Contains(searchString)));
            arenaLog[index] = $"{User.Username}\t{time.Day}.{time.Month}.{time.Year}\t{arenasPlayed}";
            await File.WriteAllLinesAsync("arenas",arenaLog);
        }

        private async Task<bool> CheckArenaBlocks(DateTime time)
        {
            var nightHours = new[] {7,8,15,16,23,0};

            if (time.Hour == 13 && time.Minute <= 1)
            {
                _arenasPlayed = 0;
                _morningQuest = false;
                //await UpdateArenasFile(0, time);
            }

            if(_arenasPlayed == 5)
                return true;
            if(nightHours.Contains(time.Hour) || time.Hour == _skipHour)
                return true;
            if(Constants.AfterBattleHours.Contains(time.Hour) && time.Minute < 9)
                return true;
            return ArenaFightStarted.AddMinutes(6) > time;
        }
    }
}