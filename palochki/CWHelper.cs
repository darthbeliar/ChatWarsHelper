﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using palochki.DB_Stuff;
using TeleSharp.TL;
using TLSharp.Core;

namespace palochki
{
    internal class CwHelper
    {
        private int _logClock;
        public UserDb User { get; }
        public UserInfo UserInfo { get; set; }
        public TelegramClient Client { get; }
        public DialogHandler CwBot { get; set; }
        public DialogHandler SavesChat { get; set; }
        public DialogHandler OrdersChat { get; set; }
        public ChannelHandler GuildChat { get; set; }
        public ChannelHandler LogChat { get; set; }
        public ChannelHandler CorovansLogChat { get; set; }
        public List<int> PreBattleCounts = new List<int>(39);
        public List<int> AfterBattleCounts = new List<int>(39);

        public CwHelper(UserDb user)
        {
            User = user;
            Client = new TelegramClient(int.Parse(user.UserTelId), user.UserTelHash,null,user.UserName);
            _logClock = 0;
        }

        public async Task InitHelper()
        {
            await Client.ConnectAsync();
            if (!Client.IsUserAuthorized())
            {
                Console.WriteLine($"\nПользователь {User.UserName} не авторизован на этом устройстве\n");
                await ExtraUtilities.AuthClient(Client);
            }
            UserInfo = Program.Db.UserInfos.FirstOrDefault(u=>u.UserId == User.Id);
            Console.WriteLine("search CWBOT\n\n");
            var botIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Constants.BotName);
            var botIds = botIdsQuery.Split('\t');
            CwBot = new DialogHandler(Client, Convert.ToInt32(botIds[0]), Convert.ToInt64(botIds[1]));
            Console.WriteLine("search CWBOT done");

            Console.WriteLine("search gi\n\n");
            if (User.GuildChatId != null)
            {
                User.GuildChatName = await ExtraUtilities.GetChannelNameById(Client, User.GuildChatId);
                await Program.Db.SaveChangesAsync();
            }
            var guildChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, User.GuildChatName);
            var guildChatIds = guildChatIdsQuery.Split('\t');
            GuildChat = new ChannelHandler(Client, Convert.ToInt32(guildChatIds[0]),
                Convert.ToInt64(guildChatIds[1]));
            if (User.GuildChatId == null)
            {
                User.GuildChatId = int.Parse(guildChatIds[0]);
                await Program.Db.SaveChangesAsync();
            }
            Console.WriteLine("search gi done");

            var savesChatIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Client.Session.TLUser.FirstName);
            var savesChatIds = savesChatIdsQuery.Split('\t');
            SavesChat = new DialogHandler(Client, Convert.ToInt32(savesChatIds[0]), Convert.ToInt64(savesChatIds[1]));

            if (User.AcceptOrders == 1)
            {
                var orderChatIds = (await ExtraUtilities.GetBotIdsByName(Client, User.OrdersChatName)).Split('\t');
                OrdersChat = new DialogHandler(Client, Convert.ToInt32(orderChatIds[0]), Convert.ToInt64(orderChatIds[1]));
            }

            await SavesChat.SendMessage("Бот перезапущен");

            if (!string.IsNullOrEmpty(User.ResultsChatName))
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

            if (User.UserName == "трунь")
            {
                var logChatIdsQuery = await ExtraUtilities.GetChannelIdsByName(Client, "TrunBanAppLogs");
                var logChatIds = logChatIdsQuery.Split('\t');
                LogChat = new ChannelHandler(Client, Convert.ToInt32(logChatIds[0]), Convert.ToInt64(logChatIds[1]));
            }

            Console.WriteLine(
                $"\nПользователь {User.UserName} подключен\nЧат ги:{User.GuildChatName}\nТриггер на мобов:{User.UserName} мобы\nКанал для реппортов караванов:{User.ResultsChatName}");
        }

        public async Task PerformStandardRoutine()
        {
            await DoLog();
            UserInfo = await Program.Db.UserInfos.FirstOrDefaultAsync(u => u.UserId == User.Id);
            await PerformFastRoutine();
            
            var lastBotMsg = await CwBot.GetLastMessage();
            var last3BotMsgs = await CwBot.GetLastMessages(3);

            await CheckForStaminaAfterBattle();
            await CheckForBattle();
            await ArenasCheck();
            await MorningQuest();
            if(User.StamaEnabled == 1)
                await UseStaminaCheck();

            if (lastBotMsg != null)
            {
                if (lastBotMsg.Message.Contains("Лучшие:"))
                {
                    await CwBot.PressButton(lastBotMsg, 0, 1);
                    Thread.Sleep(3000);
                    var res = await CwBot.GetLastMessage();
                    if (res.Message.Contains("👾Встреча"))
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, res.Id);
                }
                if (lastBotMsg.Message.Contains(Constants.Stama))
                {
                    const int afterBattleMinute = 8;
                    var time = DateTime.Now;
                    if (Constants.AfterBattleHours.Contains(time.Hour) && time.Minute < afterBattleMinute)
                        return;
                    var rng = new Random();
                    UserInfo.StamaCountToSpend = rng.Next(3, 6);
                    await Program.Db.SaveChangesAsync();
                }

                if (lastBotMsg.Message.Contains(Constants.Korovan) && User.CorovansEnabled == 1)
                    await CatchCorovan(lastBotMsg);

                if (lastBotMsg.Message.Contains(Constants.Village))
                    await MessageUtilities.SendMessage(Client, CwBot.Peer, Constants.Village);

                if (last3BotMsgs.Any(x =>
                    x.Message.Contains(Constants.HasMobs) && x.Message != UserInfo.LastFoundFight &&
                    x.FromId == Constants.CwBotId))
                {
                    var fightMessage = last3BotMsgs.First(x =>
                        x.Message.Contains(Constants.HasMobs) && x.FromId == Constants.CwBotId);
                    UserInfo.LastFoundFight = fightMessage.Message;
                    await Program.Db.SaveChangesAsync();
                    await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer,
                        fightMessage.Id);
                }
            }

            Console.WriteLine($"{DateTime.Now}: {User.UserName}: цикл проверок завершен");
        }

        private async Task DoLog()
        {
            if (User.UserName != "трунь")
                return;
            if (_logClock < 10)
            {
                _logClock++;
                return;
            }

            _logClock = 0;
            var goodLogs = new StringBuilder();
            foreach (var log in Program.Logs)
            {
                goodLogs.Append(log);
                goodLogs.Append('\n');
            }
            Program.Logs.Clear();
            await LogChat.SendMessage($"{goodLogs}ОК");
        }

        public async Task PerformFastRoutine()
        {
            await CheckControls();
            if (User.BotEnabled != 1)
                return;
            var msgsToCheck = await GuildChat.GetLastMessages(10);
            var lastGiMsg = msgsToCheck.OrderByDescending(m => m.Date).First();

            if (msgsToCheck.Any(msgToCheck =>
                string.Compare(msgToCheck?.Message, $"{User.UserName} мобы",
                    StringComparison.InvariantCultureIgnoreCase) ==
                0 && !Program.Db.UserFights.Any(u=>u.FightMsgId == msgToCheck.Id && u.UserId == User.Id)))
            {
                var msgToCheck = msgsToCheck.First(message =>
                    string.Compare(message?.Message, $"{User.UserName} мобы",
                        StringComparison.InvariantCultureIgnoreCase) == 0 &&
                    !Program.Db.UserFights.Any(u=>u.FightMsgId == message.Id && u.UserId == User.Id));
                var newFight = new UserFight {FightMsgId = msgToCheck.Id,UserDb = User, UserId = User.Id};
                Program.Db.UserFights.Add(newFight);
                await Program.Db.SaveChangesAsync();
                await HelpWithMobs(msgToCheck);
            }

            if (msgsToCheck.Any(m => m != null && m.Message.ToLower().Contains($"{User.UserName} пин".ToLower())))
                await TrySetPin(msgsToCheck.FirstOrDefault(m => m.Message.ToLower().Contains($"{User.UserName} пин".ToLower())));
            if (msgsToCheck.Any(m => m != null && m.Message.ToLower().Contains("киберчай пин")))
                await TryGlobalSetPin(msgsToCheck.FirstOrDefault(m => m.Message.ToLower().Contains("киберчай пин")));
            if (UserInfo.CyberTeaOrder != null)
            {
                await ExecuteOrder(UserInfo.CyberTeaOrder,false);
                UserInfo.CyberTeaOrder = null;
                await Program.Db.SaveChangesAsync();
            }
            if (User.AcceptOrders == 1)
                await CheckOrders();

            await CheckDepositRequest(lastGiMsg);

            if (User.UserName == "шпендаль")
            {
                await CheckBotOrder(lastGiMsg);
                await CheckHerbCommand(lastGiMsg);
            }
            
            if (User.UserName == "ефир")
            {
                await CheckGiveOrder(lastGiMsg);
                await CheckBotOrder(lastGiMsg);
                //await CheckQuestOrder();
            }
            
            if (User.UserName == "глимер")
            {
                await CheckGiveOrder(lastGiMsg,true);
            }
            await CheckTransformStockCommand(lastGiMsg);
        }

        private async Task CheckTransformStockCommand(TLMessage msgToCheck)
        {
            if (!msgToCheck.Message.ToLower().Contains($"{User.UserName} включи сдачу стока".ToLower()))
                return;
            var userInfos = Program.Db.UserInfos;
            foreach (var userInfo in userInfos)
            {
                userInfo.StockEnabled = userInfo.UserId != User.Id ? 0 : 1;
            }

            await Program.Db.SaveChangesAsync();
            await CwBot.SendMessage("/stock");
            var stockItems = (await WaitForCwBotReply()).Message + "\n";
            await CwBot.SendMessage("⚗️Алхимия");
            stockItems += (await WaitForCwBotReply()).Message;
            var stock = stockItems.Split('\n');
            var message = "";
            foreach (var stockItem in stock)
            {
                if(stockItem.Contains("Склад"))
                    continue;
                var count = stockItem.Split(" (")[1].Split(')')[0];
                var itemId = Array.IndexOf(Constants.CwItems, stockItem.Split(" (")[0]);
                message += $"{stockItem} /gd_{itemId}_{count}\n";
            }

            await GuildChat.SendMessage($"включаю сдачу стока");
            Thread.Sleep(1500);
            await GuildChat.SendMessage(message);
            Thread.Sleep(1500);
        }

        private async Task CheckQuestOrder()
        {
            var msgToCheck = await GuildChat.GetLastMessage();
            if (msgToCheck.Message.ToLower() != "сдай криса")
                return;
            if (msgToCheck.ReplyToMsgId == null)
            {
                return;
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.ToLower().Contains("/g_q_complete_"))
            {
                return;
            }

            var time = DateTime.Now;
            if (time.DayOfWeek != DayOfWeek.Tuesday && (time.Hour > 9 || time.Hour < 8))
                await GuildChat.SendMessage("сдача квестов работает только по вторникам с 8 до 9 по мск");
            await CwBot.SendMessage(replyMsg.Message);
            var lastBotMessage = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
            Program.Logs.Add($"{User.UserName} сделал хуйню с квестом");
        }

        private async Task CheckDepositRequest(TLMessage msgToCheck)
        {
            var lastMes = msgToCheck.Message;
            if(!lastMes.ToLower().Contains("/gd_"))
                return;
            UserInfo = Program.Db.UserInfos.FirstOrDefault(u => u.UserId == User.Id);
            if(UserInfo?.StockEnabled != 1)
                return;
            if(!int.TryParse(lastMes.Split("_")[1],out var id) || !int.TryParse(lastMes.Split("_")[2],out var count))
                return;
            if (User.UserName == "трунь" && id == 39)
            {
                await GuildChat.SendMessage("Неужели ты правда думаешь, что у труня так легко можно отнять сумак?");
                return;
            }
            var realId = id >= 10 ? id.ToString() : $"0{id}";
            await CwBot.SendMessage($"/gd {realId} {count}");
            var reply = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, reply.Id);
        }

        private async Task CheckHerbCommand(TLMessage msgToCheck)
        {
            if ((msgToCheck.Message.ToLower().Contains("выдай травы ") || msgToCheck.Message.ToLower().Contains("выдай трав ")) && msgToCheck.Message.Split(' ').Length == 3)
            {
                if (msgToCheck.ReplyToMsgId == null)
                {
                    await GuildChat.SendMessage("Нет реплая на травы");
                    return;
                }

                var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);

                if (!replyMsg.Message.Contains("Guild Warehouse") || !replyMsg.Message.Contains("Stinky Sumac"))
                {
                    await GuildChat.SendMessage("Нет стока трав в реплае");
                    Thread.Sleep(500);
                    return;
                }

                if (!int.TryParse(msgToCheck.Message.Split(' ')[2], out var count))
                {
                    await GuildChat.SendMessage("Не распознал число трав");
                    return;
                }
                var strings = replyMsg.Message.Split('\n');
                var commandLength = 0;
                var command = "/g_withdraw ";
                foreach (var s in strings)
                {
                    if (commandLength > 8)
                    {
                        commandLength = 0;
                        await GuildChat.SendMessage(command);
                        command = "/g_withdraw ";
                    }
                    if (s.Contains("Guild Warehouse")) continue;
                    if (!int.TryParse(s.Split(' ')[0], out var herbId)) continue;
                    if (!int.TryParse(s.Split("x ")[1], out var herbCount)) continue;
                    if (herbCount <= count) continue;
                    command += $"{herbId} {herbCount-count} ";
                    commandLength++;
                }

                if (commandLength > 0)
                {
                    await GuildChat.SendMessage(command);
                    Program.Logs.Add($"{User.UserName} сделал хуйню с травами");
                    Thread.Sleep(1000);
                }
            }
        }

        private async Task UseStaminaCheck()
        {
            var time = DateTime.Now;
            if (Constants.AfterBattleHours.Contains(time.Hour) && time.Minute < 7)
                return;
            if (Constants.BattleHours.Contains(time.Hour) && time.Minute > 52)
                return;
            if (UserInfo.StamaCountToSpend == 0)
                return;
            var botMsg = (await CwBot.GetLastMessage()).Message;
            if (botMsg.Contains("Горы полны опасностей") || botMsg.Contains("Ты отправился искать приключения в лес") ||
                botMsg.Contains("ты отправился в болото"))
                return;
            if (botMsg.Contains("Слишком мало единиц выносливости"))
            {
                UserInfo.StamaCountToSpend = 0;
                await Program.Db.SaveChangesAsync();
                return;
            }

            var waitMins = 4;
            if (Constants.NightHours.Contains(time.Hour))
                waitMins = 6;
            if (UserInfo.QuestType == 4)
                waitMins += 2;
            var stamaUseStarted = ParseDbDate(UserInfo.StamaUseStarted);
            if(time<stamaUseStarted.AddMinutes(waitMins))
                return;
            await UseStamina();

            botMsg = (await CwBot.GetLastMessage()).Message;
            if (botMsg.Contains("Горы полны опасностей") || botMsg.Contains("Ты отправился искать приключения в лес") ||
                botMsg.Contains("ты отправился в болото"))
            {
                UserInfo.StamaCountToSpend--;
            }
            UserInfo.StamaUseStarted = AddTimeToDb(time);
            await Program.Db.SaveChangesAsync();
            if (botMsg.Contains("подлечиться"))
            {
                UserInfo.StamaUseStarted = AddTimeToDb(time.AddMinutes(10));
                await Program.Db.SaveChangesAsync();
            }
        }

        private string AddTimeToDb(in DateTime time)
        {
            return $"{time.Year} {time.Month} {time.Day} {time.Hour} {time.Minute} {time.Second}";
        }

        private DateTime ParseDbDate(string? userInfoStamaUseStarted)
        {
            var split = userInfoStamaUseStarted.Split(' ');
            return new DateTime(int.Parse(split[0]),int.Parse(split[1]),int.Parse(split[2]),int.Parse(split[3]),int.Parse(split[4]),int.Parse(split[5]));
        }

        private async Task CheckGiveOrder(TLMessage msgToCheck,bool alch = false)
        {
            if (msgToCheck.Message.ToLower() != "дай ефир" && msgToCheck.Message.ToLower() != "налей глимер")
                return;
            switch (msgToCheck.Message.ToLower())
            {
                case "дай ефир" when alch:
                case "налей глимер" when alch == false:
                    return;
            }

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
            var lastBotMessage = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
            Program.Logs.Add($"{User.UserName} сделал хуйню с выдачей");
        }

        private async Task MorningQuest()
        {
            var time = DateTime.Now;
            if (UserInfo.MorningQuest == 1 || User.StamaEnabled != 1)
                return;
            if (time.Hour == 8 && time.Minute > 10 && time.Minute < 14)
            {
                await UseStamina();
                UserInfo.MorningQuest = 1;
                await Program.Db.SaveChangesAsync();
                Program.Logs.Add($"{User.UserName} сделал хуйню с утреним квестом");
            }
        }

        private async Task CheckBotOrder(TLMessage msgToCheck)
        {
            if (msgToCheck.Message.ToLower() != "в бота")
                return;
            if (msgToCheck.ReplyToMsgId == null)
            {
                await GuildChat.SendMessage("нужен реплай");
                return;
            }

            var replyMsg = await GuildChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(2000);
            var lastBotMessage = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);
        }

        private async Task CheckOrders()
        {
            var lastMes = await OrdersChat.GetLastMessage();
            if (lastMes.Message.ToLower().Contains($"{User.UserName} пин"))
                await TrySetPin(lastMes,true);
            if (lastMes.Message.ToLower().Contains("выпей рагу"))
                await TryDrinkRage();
            if (lastMes.Message.ToLower().Contains("бери"))
                await TryTakeItems(lastMes);
            if (lastMes.Message.ToLower().Contains("положи"))
                await TryDepositItems(lastMes);
            if (lastMes.Message.ToLower().Contains("скинь героя"))
                await GetHeroMessage();
        }

        private async Task GetHeroMessage()
        {
            await CwBot.SendMessage(Constants.HeroCommand);
            var lastBotMessage = await WaitForCwBotReply();
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
            var lastBotMessage = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, lastBotMessage.Id);
        }

        private async Task TryDepositItems(TLMessage msgToCheck)
        {
            if (msgToCheck.ReplyToMsgId == null)
            {
                await OrdersChat.SendMessage("Нет реплая на сообщение");
                return;
            }

            var replyMsg = await OrdersChat.GetMessageById(msgToCheck.ReplyToMsgId.Value);
            if (!replyMsg.Message.Contains("/gd_"))
            {
                await OrdersChat.SendMessage("Нет ссылки на итемы");
                return;
            }

            await CwBot.SendMessage(replyMsg.Message);
            var lastBotMessage = await WaitForCwBotReply();
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, lastBotMessage.Id);
        }

        private async Task TryDrinkRage()
        {
            await CwBot.SendMessage("/misc rage");
            var reply = await WaitForCwBotReply();
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
                reply = await WaitForCwBotReply();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
                await CwBot.SendMessage("/use_p02");
                reply = await WaitForCwBotReply();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
                await CwBot.SendMessage("/use_p03"); ;
                reply = await WaitForCwBotReply();
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
            }
            else
            {
                await OrdersChat.SendMessage("До битвы больше чем полчаса");
            }
        }

        private async Task TryGlobalSetPin(TLMessage msg)
        {
            if(User.UserName != "трунь" || msg.Id == UserInfo.LastBadRequestId)
                return;
            var parsed = msg.Message.Split(' ');

            if (parsed.Length != 3)
            {
                await GuildChat.SendMessage("Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
                UserInfo.LastBadRequestId = msg.Id;
                await Program.Db.SaveChangesAsync();
                return;
            }

            var pin = msg.Message.Split(' ')[2];

            if (!Constants.Castles.Contains(pin) && !pin.Contains("_atk") && !pin.Contains("_def") && !pin.Contains("🛡Защита"))
            {
                await GuildChat.SendMessage("не распознал пин");
                UserInfo.LastBadRequestId = msg.Id;
                await Program.Db.SaveChangesAsync();
                return;
            }

            UserInfo.LastBadRequestId = msg.Id;
            var cyberTea = Program.Db.UserInfos.Where(ui => ui.IsCyberTea == 1);
            foreach (var userInfo in cyberTea)
            {
                userInfo.CyberTeaOrder = pin;
            }
            await Program.Db.SaveChangesAsync();
            Program.Logs.Add($"{User.UserName} сделал хуйню с массовым пином");
        }
        private async Task TrySetPin(TLMessage msg,bool personalOrder = false)
        {
            if(msg.Id == UserInfo.LastBadRequestId)
                return;
            var parsed = msg.Message.Split(' ');

            if (parsed.Length != 3)
            {
                if (personalOrder)
                    await OrdersChat.SendMessage(
                        "Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
                else 
                    await GuildChat.SendMessage("Неверный формат команды. Должна состоять из 3 слов через пробел(имя пин цель)");
                UserInfo.LastBadRequestId = msg.Id;
                await Program.Db.SaveChangesAsync();
                return;
            }

            var pin = msg.Message.Split(' ')[2];
            await ExecuteOrder(pin, personalOrder);
            

            UserInfo.LastBadRequestId = msg.Id;
            await Program.Db.SaveChangesAsync();
            Program.Logs.Add($"{User.UserName} сделал хуйню с пином");
        }

        private async Task ExecuteOrder(string pin,bool personalOrder)
        {
            if (Constants.Castles.Contains(pin))
            {
                var attackWord2 = new char[6];
                attackWord2[0] = Convert.ToChar(9876);
                attackWord2[1] = Convert.ToChar(1040);
                attackWord2[2] = Convert.ToChar(1090);
                attackWord2[3] = Convert.ToChar(1072);
                attackWord2[4] = Convert.ToChar(1082);
                attackWord2[5] = Convert.ToChar(1072);
                await CwBot.SendMessage(new string(attackWord2));
                Thread.Sleep(2000);
                var replyToAttack = await CwBot.GetLastMessage();
                if (replyToAttack.Message != "Смелый вояка! Выбирай врага")
                {
                    if(personalOrder)
                        await OrdersChat.SendMessage(replyToAttack.Message);
                    else
                        await GuildChat.SendMessage(replyToAttack.Message);
                    return;
                }
            }
            else if (!pin.Contains("_atk") && !pin.Contains("_def") && !pin.Contains("🛡Защита"))
            {
                if(personalOrder)
                    await OrdersChat.SendMessage("не распознал пин");
                else
                    await GuildChat.SendMessage("не распознал пин");
                return;
            }

            await CwBot.SendMessage(pin);
            var reply = await WaitForCwBotReply();
            if(personalOrder)
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, OrdersChat.Peer, reply.Id);
            else
                await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, reply.Id);
            Program.Logs.Add($"{User.UserName} сделал хуйню с пином");
        }

        private async Task CheckControls()
        {
            var lastMsg = await SavesChat.GetLastMessage();

            switch (lastMsg.Message)
            {
                case "help":
                    await SavesChat.SendMessage(
                        "stop bot = полностью выключить бота\nstart bot = полностью включить бота\n" +
                        "enable arenas = включить автоарены\ndisable arenas = выключить автоарены\n" +
                        "enable stama = включить автослив стамины\ndisable stama = выключить автослив стамины\n" +
                        "use stamina х = слить х стамины\n" +
                        "enable def = включить автогидеф\ndisable def = выключить автогидеф\n" +
                        "bot status = состояние функций бота\n" +
                        "disable potions = выключить автозелья на чемпа\nenable potions = включить автозелья на чемпа\n" +
                        "enable corovans = включить автостоп корованов\ndisable corovans = выключить автостоп корованов" +
                        "\nset autoquest x, 1 = лес, 2 = болото, 3 = долина, 4 = корованы");
                    await SavesChat.SendMessage(
                        "Доп команды: \n[юзер] пин [цель] \nдай криса реплаем в чате чая \n выдай трав [x] в ботодельне \n[юзер] покажи сток \n[юзeр] положи x y");
                    break;
                case "bot status":
                    await SavesChat.SendMessage(
                        $"бот = {(User.BotEnabled != 1 ? "выключен" : "включен")}\nарены = {(User.ArenasEnabled != 1 ? "выключены" : "включены")}\n" +
                        $"автослив стамины = {(User.StamaEnabled != 1 ? "выключен" : "включен")}\nавтогидеф = {(User.AutoGDefEnabled != 1 ? "выключен" : "включен")}" +
                        $"\nзелья на чемпа = {(User.PotionsEnabled != 1 ? "выключены" : "включены")}\nкорованы = {(User.CorovansEnabled != 1 ? "выключены" : "включены")}" +
                        $"\nквест = {UserInfo.QuestType}");
                    break;
                case "stop bot":
                    await SavesChat.SendMessage("Бот остановлен");
                    User.BotEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "start bot":
                    await SavesChat.SendMessage("Бот запущен");
                    User.BotEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable arenas":
                    await SavesChat.SendMessage("Автоарены включены");
                    User.ArenasEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "disable arenas":
                    await SavesChat.SendMessage("Автоарены выключены");
                    User.ArenasEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable stama":
                    await SavesChat.SendMessage("Автослив стамины включен");
                    User.StamaEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "disable stama":
                    await SavesChat.SendMessage("Автослив стамины выключен");
                    User.StamaEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable potions":
                    User.PotionsEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "disable potions":
                    User.PotionsEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable def":
                    await SavesChat.SendMessage("Автогдеф включен");
                    User.AutoGDefEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "disable def":
                    await SavesChat.SendMessage("Автогдеф выключен");
                    User.AutoGDefEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable corovans":
                    await SavesChat.SendMessage("Автостоп корованов включен");
                    User.CorovansEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "disable corovans":
                    await SavesChat.SendMessage("Автогдеф корованов выключен");
                    User.CorovansEnabled = 0;
                    await Program.Db.SaveChangesAsync();
                    break;
                case "enable all":
                    await SavesChat.SendMessage("Все функции активированы");
                    User.AutoGDefEnabled = 1;
                    User.StamaEnabled = 1;
                    User.ArenasEnabled = 1;
                    User.PotionsEnabled = 1;
                    User.CorovansEnabled = 1;
                    await Program.Db.SaveChangesAsync();
                    break;
            }

            if (lastMsg.Message.ToLower().Contains("set autoquest"))
            {
                if (int.TryParse(lastMsg.Message.Split(' ')[2], out var qId))
                {
                    if (qId < 1 || qId > 4)
                    {
                        await SavesChat.SendMessage("Неверный айди квеста");
                        return;
                    }

                    UserInfo.QuestType = qId;
                    await Program.Db.SaveChangesAsync();
                    await SavesChat.SendMessage($"установлен тип квеста {qId}");
                }
            }
            if (lastMsg.Message.ToLower().Contains("use stamina"))
            {
                if (int.TryParse(lastMsg.Message.Split(' ')[2], out var count))
                {
                    if(UserInfo.QuestType == 4)
                        count /= 2;
                    UserInfo.StamaCountToSpend = count;
                    await Program.Db.SaveChangesAsync();
                    await SavesChat.SendMessage($"сливаю {count} стамы");
                }
            }
        }

        private async Task CheckForStaminaAfterBattle()
        {
            
            if (User.StamaEnabled != 1)
                return;
            const int afterBattleMinute = 8;
            var time = DateTime.Now;
            if (Constants.AfterBattleHours.Contains(time.Hour) && time.Minute == afterBattleMinute)
            {
                if (UserInfo.AfterBattleLock != 1)
                {
                    await CwBot.SendMessage(Constants.GetReportCommand);
                    Thread.Sleep(1500);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.ReportsHeader))
                        await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, botReply.Id);

                    Console.WriteLine($"{DateTime.Now}: {User.UserName}: репорт отправлен");
                    Program.Logs.Add($"{User.UserName} сделал хуйню с репортом");

                    if (!string.IsNullOrEmpty(User.ResultsChatName))
                    {
                        Thread.Sleep(2000);
                        await CwBot.SendMessage("/g_stock_res");
                        Thread.Sleep(3000);
                        botReply = await CwBot.GetLastMessage();
                        if (botReply.Message.Contains("Guild Warehouse"))
                        {
                            AfterBattleCounts = ParseStock(botReply.Message);
                            var stockSize = botReply.Message.Split("Guild Warehouse: ")[1].Split('\n')[0];
                            Program.Logs.Add($"{User.UserName} сделал хуйню с стоком после битвы {stockSize}");
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
                                var sign = change > 0 ? "+" : "";
                                msg += $"{Constants.CwItems[i]} {sign}{change}\n";
                            }

                            if (!noChanges)
                            {
                                await CorovansLogChat.SendMessage(msg);
                                Program.Logs.Add($"{User.UserName} сделал хуйню с пиратсвом +");
                            }
                            else
                            {
                                Program.Logs.Add($"{User.UserName} сделал хуйню с пиратсвом -");
                            }

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

                    UserInfo.AfterBattleLock = 1;
                    await Program.Db.SaveChangesAsync();
                }
            }
            else
            {
                UserInfo.AfterBattleLock = 0;
                await Program.Db.SaveChangesAsync();
            }
        }

        private async Task UseStamina()
        {
            if(User.StamaEnabled != 1)
                return;
            await CwBot.SendMessage(Constants.QuestsCommand);
            var rowId = 0;
            var botReply = await WaitForCwBotReply();
            var buttonNumber = 2;
            if (botReply.Message.Contains(Constants.ForestQuestForRangers) || botReply.Message.Contains(Constants.ForestQuestForRangersN))
                buttonNumber = 0;
            if (botReply.Message.Contains(Constants.SwampQuestForRangers) || botReply.Message.Contains(Constants.SwampQuestForRangersN))
                buttonNumber = 1;
            if (buttonNumber == 2 || User.UserName == "ефир")
            {
                switch (UserInfo.QuestType)
                {
                    case 1:
                        buttonNumber = 0;
                        break;
                    case 2:
                        buttonNumber = 1;
                        break;
                    case 3:
                        buttonNumber = 2;
                        break;
                    case 4:
                        buttonNumber = 0;
                        rowId = 1;
                        break;
                }
            }
            Thread.Sleep(1000);
            await CwBot.PressButton(botReply, rowId, buttonNumber);
            await WaitForCwBotReply();
            Program.Logs.Add($"{User.UserName} сделал хуйню с стаминой");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.UserName} юзнул автослив стамы\n");
        }

        private async Task<TLMessage> WaitForCwBotReply()
        {
            var lastMsg = await CwBot.GetLastMessage();
            var tries = 0;
            while (lastMsg.FromId != Constants.CwBotId && tries < 15)
            {
                Thread.Sleep(1000);
                lastMsg = await CwBot.GetLastMessage();
                tries++;
            }
            Thread.Sleep(500);
            return lastMsg;
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
            Program.Logs.Add($"{User.UserName} сделал хуйню с караваном");
            Console.WriteLine($"{DateTime.Now}: {User.UserName}: пойман корован");
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
            lastBotMessage = await WaitForCwBotReply();
            var hp = lastBotMessage.Message.Split("❤️Здоровье: ")[1].Split('/')[0];
            var maxHp = lastBotMessage.Message.Split("❤️Здоровье: ")[1].Split('/')[1].Split('\n')[0];
            var coef = double.Parse(hp) / double.Parse(maxHp);

            if (lastBotMessage.Message.Contains("🎯"))
            {
                var aimMes = lastBotMessage.Message.Split("Состояние:")[1].Split("Подробнее")[0];
                await GuildChat.SendMessage($"не могу, в аиме\n{aimMes}");
                return;
            }

            if (coef > 0.4 &&
                int.TryParse(lastBotMessage.Message.Split("Уровень: ")[1].Substring(0, 2), out var lvl))
            {
                await HelpIfMobsNotTooBig(lvl, replyMsg);
            }
            else
            {
                if (msgToCheck.FromId == 255464103)
                    await GuildChat.SendMessage("лучше бы /g_q_discard_a10 нажала чем пытаться убить лоухпшного криса");
                else
                {
                    var lowHpReplies = Program.Db.LowHpReplies.Select(x=>x.Reply).ToArray();
                    var rng = new Random();
                    await GuildChat.SendMessage(lowHpReplies[rng.Next(0,lowHpReplies.Length-1)]);
                }
            }
            Program.Logs.Add($"{User.UserName} сделал хуйню с мобами");
        }

        private async Task HelpIfMobsNotTooBig(int lvl, TLMessage replyMsg)
        {
            var minLvl = 999;
            var maxLvl = 1;
            var mobLvlsFromMsg = replyMsg.Message.Split("lvl.");
            foreach (var s in mobLvlsFromMsg)
            {
                if (int.TryParse(s.Substring(0, 2), out var moblvl))
                {
                    if (moblvl < minLvl)
                        minLvl = moblvl;
                    if (moblvl > maxLvl)
                        maxLvl = moblvl;
                }
            }

            if (lvl - minLvl > 10)
            {
                await GuildChat.SendMessage($"мобы слишком мелкие,мой лвл {lvl}, самый мелкий моб в пачке {minLvl}");
                return;
            }
            if(maxLvl - lvl > 12 && !replyMsg.Message.Contains("Forbidden Champion"))
            {
                await GuildChat.SendMessage($"мобы слишком большие, мой лвл {lvl}, самый большой моб в пачке {maxLvl}");
                return;
            }
            Thread.Sleep(1000);
            await CwBot.SendMessage(replyMsg.Message);
            Thread.Sleep(1000);
            var lastBotMessage = await CwBot.GetLastMessage();
            if(lastBotMessage.FromId != Constants.CwBotId)
            {
                await CwBot.SendMessage(replyMsg.Message); 
                Thread.Sleep(2000);
                lastBotMessage = await CwBot.GetLastMessage();
                if (lastBotMessage.FromId != Constants.CwBotId)
                {
                    await CwBot.SendMessage(replyMsg.Message); 
                    Thread.Sleep(3000);
                    lastBotMessage = await CwBot.GetLastMessage();
                    if (lastBotMessage.FromId != Constants.CwBotId)
                    {
                        await GuildChat.SendMessage("ЧВ не схавал моба");
                        return;
                    }
                }
            }
            await MessageUtilities.ForwardMessage(Client, CwBot.Peer, GuildChat.Peer, lastBotMessage.Id);

            if (replyMsg.Message.Contains("Forbidden Champion") &&
                lastBotMessage.Message.Contains("собрался напасть") && User.PotionsEnabled == 1)
                await DrinkPotions();

            Console.WriteLine($"{DateTime.Now}: {User.UserName}: помог с мобами");
            Program.Logs.Add($"{User.UserName} сделал хуйню с помощью");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.UserName} помог с мобами\n");
        }

        private async Task DrinkPotions()
        {
            await CwBot.SendMessage("/inv");
            await WaitForCwBotReply();
            await CwBot.SendMessage("🗃Другое");
            var botReply = (await WaitForCwBotReply()).Message;
            if (Constants.RagePots.Any(p => botReply.Contains(p)) || Constants.DefPots.Any(p => botReply.Contains(p)))
            {
                await CwBot.SendMessage("/g_withdraw p01 1 p02 1 p03 1 p04 1 p05 1 p06 1");

                botReply = (await WaitForCwBotReply()).Message;
                if (!botReply.Contains("Withdrawing:"))
                {
                    await GuildChat.SendMessage("Нет зелий в стоке или прав на их получение");
                    return;
                }

                await CwBot.SendMessage(botReply);
                await WaitForCwBotReply();
            }


            await CwBot.SendMessage("/use_p01");
            await WaitForCwBotReply();
            await CwBot.SendMessage("/use_p02");
            await WaitForCwBotReply();
            await CwBot.SendMessage("/use_p03");
            await WaitForCwBotReply();
            await CwBot.SendMessage("/use_p04");
            await WaitForCwBotReply();
            await CwBot.SendMessage("/use_p05");
            await WaitForCwBotReply();
            await CwBot.SendMessage("/use_p06");
            await WaitForCwBotReply();
            await GuildChat.SendMessage("выпил зелья");
            Program.Logs.Add($"{User.UserName} сделал хуйню с банками");
        }

        private async Task CheckForBattle()
        {
            if(User.AutoGDefEnabled != 1)
                return;
            const int battleMinute = 58;
            var time = DateTime.Now;
            if (Constants.BattleHours.Contains(time.Hour) && time.Minute >= battleMinute)
            {
                if (UserInfo.BattleLock != 1)
                {
                    Thread.Sleep(1500);
                    await CwBot.SendMessage(Constants.HeroCommand);
                    Thread.Sleep(2000);
                    var botReply = await CwBot.GetLastMessage();
                    if (botReply.Message.Contains(Constants.RestedState) || botReply.Message.Contains(Constants.SmithState))
                    {
                        if(User.UserName == "наста")
                            await CwBot.SendMessage("🛡Защита");
                        else
                            await CwBot.SendMessage("/g_def");
                        Thread.Sleep(2000);
                        Console.WriteLine($"{DateTime.Now}: {User.UserName}: ушел в гидеф");
                        await File.AppendAllTextAsync(Constants.ActionLogFile,
                            $"{DateTime.Now}\n{User.UserName} сходил в автогидеф\n");
                        Program.Logs.Add($"{User.UserName} сделал хуйню с гидефом");
                    }

                    if (!string.IsNullOrEmpty(User.ResultsChatName))
                    {
                        await CwBot.SendMessage("/g_stock_res");
                        Thread.Sleep(2000);
                        botReply = await CwBot.GetLastMessage();
                        if (botReply.Message.Contains("Guild Warehouse"))
                        {
                            PreBattleCounts = ParseStock(botReply.Message);
                            var stockSize = botReply.Message.Split("Guild Warehouse: ")[1].Split('\n')[0];
                            Program.Logs.Add($"{User.UserName} сделал хуйню с стоком перед битвой {stockSize}");
                        }
                    }
                    //мемнулся, /F кольцу
                    /*
                    if (User.Username == "белиар")
                    {
                        await CheckRing("u127");
                    }
                    */
                    UserInfo.BattleLock = 1;
                    await Program.Db.SaveChangesAsync();
                }
            }
            else
            {
                UserInfo.BattleLock = 0;
                await Program.Db.SaveChangesAsync();
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
            else
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
            if(User.ArenasEnabled != 1)
                return;
            var time = DateTime.Now;
            if(User.UserName == "глимер" && (time.Hour <10 || time.Hour > 13))
                return;
            if(await CheckArenaBlocks(time)) return;
            if(UserInfo.ArenasPlayed > 4)
                return;
            UserInfo.SkipHour = 25;
            await Program.Db.SaveChangesAsync();

            await CwBot.SendMessage(Constants.QuestsCommand);
            var botReply = await WaitForCwBotReply();
            Thread.Sleep(1000);
            await CwBot.PressButton(botReply, 1, 1);
            var lastId = botReply.Id;
            while (botReply.Id == lastId)
            {
                botReply = await CwBot.GetLastMessage();
                Thread.Sleep(1000);
            }

            if (botReply.Message == Constants.BusyState)
            {
                UserInfo.SkipHour = (byte) time.Hour;
                await Program.Db.SaveChangesAsync();
                return;
            }
            UserInfo.ArenasPlayed = ExtraUtilities.ParseArenasPlayed(botReply.Message);
            await Program.Db.SaveChangesAsync();

            if (UserInfo.ArenasPlayed == 5)
                return;

            await CwBot.SendMessage(Constants.FastFightCommand);
            botReply = await WaitForCwBotReply();
            if (botReply.Message != Constants.SuccessArenaStart)
            {
                UserInfo.SkipHour = (byte) time.Hour;
                await Program.Db.SaveChangesAsync();
            }

            if (UserInfo.ArenasPlayed == 4)
            {
                UserInfo.ArenasPlayed = 5;
                await Program.Db.SaveChangesAsync();
            }

            Console.WriteLine($"{DateTime.Now}: {User.UserName}: ушел на арену");
            await File.AppendAllTextAsync(Constants.ActionLogFile,
                $"{DateTime.Now}\n{User.UserName} сходил на автоарену\n");
            Program.Logs.Add($"{User.UserName} сделал хуйню с ареной");
            UserInfo.ArenaFightStarted = AddTimeToDb(time);
            await Program.Db.SaveChangesAsync();
        }

        private async Task<bool> CheckArenaBlocks(DateTime time)
        {
            if (time.Hour == 13 && time.Minute <= 1)
            {
                if (User.UserName == "шпендаль" && UserInfo.ArenasPlayed != 0)
                    await CheckBottles(507, 506);
                if (User.UserName == "наста" && UserInfo.ArenasPlayed != 0)
                    await CheckBottles(509, 508);
                UserInfo.ArenasPlayed = 0;
                UserInfo.MorningQuest = 0;
                await Program.Db.SaveChangesAsync();
            }

            if(UserInfo.ArenasPlayed == 5)
                return true;
            if(Constants.NightHours.Contains(time.Hour) || time.Hour == UserInfo.SkipHour)
                return true;
            if(Constants.AfterBattleHours.Contains(time.Hour) && time.Minute < 9)
                return true;
            return ParseDbDate(UserInfo.ArenaFightStarted).AddMinutes(6) > time;
        }

        private async Task CheckBottles(int packId, int itemsId)
        {
            var needMoreBottles = false;
            var noBottlesEquipped = false;
            await CwBot.SendMessage("/inv");
            var botReply = (await WaitForCwBotReply()).Message;
            if (botReply.Contains($"/off_{itemsId}"))
            {
                if (int.TryParse(botReply.Split("Bottle of ")[1].Split('(')[1].Split(')')[0], out var count))
                    if (count < 50)
                        needMoreBottles = true;
            }
            else
            {
                needMoreBottles = true;
                noBottlesEquipped = true;
            }

            if (needMoreBottles)
            {
                await CwBot.SendMessage($"/use_{packId} 10");
                await WaitForCwBotReply();
                if (noBottlesEquipped)
                {
                    await CwBot.SendMessage($"/on_{itemsId}");
                    await WaitForCwBotReply();
                }
            }
            Program.Logs.Add($"{User.UserName} сделал хуйню с банками алхов");
        }
    }
}