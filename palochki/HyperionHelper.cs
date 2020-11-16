using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TLSharp.Core;

namespace palochki
{
    internal class HyperionHelper
    {
        public User User { get; }
        public TelegramClient Client { get; set; }
        public DialogHandler HyperionBot { get; set; }
        public DialogHandler SavesChat { get; set; }
        private List<string> _mobsDamage;
        private bool _disabled;
        private short _farmSpot;
        private bool _farmInProcess;
        private bool _timeToGoHome;
        private bool _outOfFood;
        private short _xTarget;
        private short _yTarget;
        private short _x;
        private short _y;
        private short _energy;
        private short _food;
        private short _manaForHeal;
        private short _hp;
        private short _foodId;
        private bool _rested;
        private int _lastComMesId;

        private static string[] _bannedStrings;
        private short _zigCount;
        private string _zigDir;

        public HyperionHelper(User user)
        {
            User = user;
            _disabled = true;
            _farmInProcess = false;
            _timeToGoHome = false;
            _outOfFood = false;
            _farmSpot = 11;
            _xTarget = 100;
            _yTarget = 100;
            _x = 0;
            _y = 0;
            _hp = 0;
            _energy = 0;
            _food = 0;
            _manaForHeal = 1000;
            _foodId = 101;
            _rested = false;
            _zigCount = 0;
            _lastComMesId = 0;
        }

        public async Task InitHelper(TelegramClient client)
        {
            if (!User.HyperionUser)
            {
                _disabled = true;
                return;
            }

            _bannedStrings = await File.ReadAllLinesAsync("bannedStrings");
            Client = client;
            var botIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Constants.HyperionBotName);
            var botIds = botIdsQuery.Split('\t');
            HyperionBot = new DialogHandler(Client, Convert.ToInt32(botIds[0]), Convert.ToInt64(botIds[1]));

            var savesChatIdsQuery = await ExtraUtilities.GetBotIdsByName(Client, Client.Session.TLUser.FirstName);
            var savesChatIds = savesChatIdsQuery.Split('\t');
            SavesChat = new DialogHandler(Client, Convert.ToInt32(savesChatIds[0]), Convert.ToInt64(savesChatIds[1]));

            _mobsDamage = (await File.ReadAllLinesAsync(Constants.HyperionSettingsFileName + '_' + User.Username)).ToList();
            _farmSpot = short.Parse(await File.ReadAllTextAsync("farm_spot_" + User.Username));
            _foodId = short.Parse(await File.ReadAllTextAsync("food_id_" + User.Username));
            _manaForHeal = short.Parse(await File.ReadAllTextAsync("manaForHeal_" + User.Username));
            var state = (await File.ReadAllLinesAsync("HypState_" + User.Username)).ToList();
            _disabled = state[0] == "True";
            _farmInProcess = state[1] == "True";
            _timeToGoHome = state[2] == "True";
            _outOfFood = state[3] == "True";
            _rested = state[4] == "True";
            _xTarget = short.Parse(state[5]);
            _yTarget = short.Parse(state[6]);
            _zigCount = short.Parse(state[7]);
            _zigDir = state[8];

            Console.WriteLine(
                $"\nПользователь {User.Username} подключен к Гипериону\n");
        }

        public async Task DoFarm()
        {
            await CheckControls();
            await SaveState();
            var lastMsg = await HyperionBot.GetLastMessage();

            var lastMsgs = await HyperionBot.GetLastMessages(4);
            if (lastMsgs.Any(m=>m.Message.Contains("У тебя есть две минуты на ответ")))
            {
                var msg = lastMsgs.FirstOrDefault(m => m.Message.Contains("У тебя есть две минуты на ответ"));
                await HyperionBot.PressButton(msg, 0, 0);
                return;
            }

            if (_disabled && _xTarget == 100 && _zigCount == 0)
                return;

            await UpdateCharStats();

            foreach (var bannedString in _bannedStrings)
            {
                if (!lastMsg.Message.Contains(bannedString)) continue;

                var hist = (await HyperionBot.GetLastMessages(20)).OrderByDescending(m=>m.Date).ToArray();
                foreach (var mes in hist)
                {
                    var isBadToo = false;
                    foreach (var bannedString2 in _bannedStrings)
                        if (mes.Message.Contains(bannedString2))
                            isBadToo = true;
                    if (!isBadToo && mes.FromId == HyperionBot.Peer.UserId)
                    {
                        lastMsg = mes;
                        break;
                    }

                    if (mes.Message.Contains("У тебя нет в наличии eды"))
                        _outOfFood = true;
                    if (mes.Message.Contains("отдохнул и можешь"))
                        _rested = true;
                }
            }

            if(lastMsg.Message.Contains("Ты напал на моба") || lastMsg.Message.Contains("Ты отправился в дорогу."))
                return;

            if (lastMsg.Message.Contains("Перед тобой стоит"))
            {
                await Fight();
                return;
            }

            if (lastMsg.Message.Contains("Внимание, вы перегружены и не можете идти."))
            {
                await HyperionBot.SendMessage($"🛡 Оружие/броня");
                Thread.Sleep(1500);
                var inv = (await HyperionBot.GetLastMessage()).Message;
                var delCommand = inv.Split("🗑: ")[1].Split('\n')[0];
                await HyperionBot.SendMessage(delCommand);
                Thread.Sleep(1500);
            }

            if (_food == 0 && !_outOfFood)
            {
                if (lastMsg.Message.Contains("Ты съел"))
                    _food = 1;
                else
                {
                    await HyperionBot.SendMessage("/food");
                    Thread.Sleep(1500);
                    var foodReply = (await HyperionBot.GetLastMessage()).Message;
                    if (foodReply.Contains($"/eat_{_foodId}"))
                    {
                        await HyperionBot.SendMessage($"/eat_{_foodId}");
                        Thread.Sleep(2000);
                        var mes = await HyperionBot.GetLastMessage();
                        if (mes.Message.Contains("У тебя нет в наличии eды"))
                            _outOfFood = true;
                    }
                    else
                        _outOfFood = true;
                }
            }

            if (_energy == 0)
            {
                if (_rested)
                    _rested = false;
                else
                    return;
            }

            if (_xTarget != 100)
            {
                if (_xTarget == _x && _yTarget == _y)
                {
                    _xTarget = 100;
                    await SavesChat.SendMessage("пришли");
                    return;
                }

                await DoStep(CalculateDirection());
                return; 
            }

            if (_zigCount > 0)
            {
                if (Math.Abs(_x) == Math.Abs(_y))
                {
                    _zigCount = 0;
                    await SavesChat.SendMessage("дошли до угла");
                    return;
                }
                _zigCount--;
                switch (_zigDir)
                {
                    case "up":
                        if (_x % 2 == 0 && _y % 2 == 0 || _x % 2 != 0 && _y % 2 != 0)
                            await DoStep("⬆️ Север");
                        else if (_x % 2 != 0 && _y % 2 == 0 && _y > 0 || _x % 2 == 0 && _y % 2 != 0 && _y < 0)
                            await DoStep("⬅️ Запад");
                        else 
                            await DoStep("➡️ Восток");
                        break;
                    case "down":
                        if (_x % 2 == 0 && _y % 2 == 0 || _x % 2 != 0 && _y % 2 != 0)
                            await DoStep("⬇️ Юг");
                        else if (_x % 2 != 0 && _y % 2 == 0 && _y > 0 || _x % 2 == 0 && _y % 2 != 0 && _y < 0)
                            await DoStep("⬅️ Запад");
                        else 
                            await DoStep("➡️ Восток");
                        break;
                    case "left":
                        if (_x % 2 == 0 && _y % 2 == 0 || _x % 2 != 0 && _y % 2 != 0)
                            await DoStep("⬅️ Запад");
                        else if (_x%2 == 0 && _y%2 != 0 && _x < 0 || _x%2 != 0 && _y%2 == 0 && _x > 0)
                            await DoStep("⬆️ Север");
                        else 
                            await DoStep("⬇️ Юг");
                        break;
                    case "right":
                        if (_x % 2 == 0 && _y % 2 == 0 || _x % 2 != 0 && _y % 2 != 0)
                            await DoStep("➡️ Восток");
                        else if (_x%2 == 0 && _y%2 != 0 && _x < 0 || _x%2 != 0 && _y%2 == 0 && _x > 0)
                            await DoStep("⬆️ Север");
                        else 
                            await DoStep("⬇️ Юг");
                        break;
                }

                if (_zigCount == 0)
                    await SavesChat.SendMessage("Зигзаг окончен");
                return;
            }

            if (!_farmInProcess && (_x != _y || _x > _farmSpot))
            {
                _disabled = true;
                await SavesChat.SendMessage("Для начала фарма нужно быть в городе или основной диагонали ниже места фарма");
                return;
            }
            
            _farmInProcess = true;

            await CheckHp();

            if (_timeToGoHome)
            {
                if (CharInTown())
                    await DoRegen();
                else
                    await DoStepToTown();
            }
            else
            {
                await DoStepToFarmSpot();
            }
        }

        private async Task SaveState()
        {
            var state = new string[9];
            state[0] = _disabled.ToString();
            state[1] = _farmInProcess.ToString();
            state[2] = _timeToGoHome.ToString();
            state[3] = _outOfFood.ToString();
            state[4] = _rested.ToString();
            state[5] = _xTarget.ToString();
            state[6] = _yTarget.ToString();
            state[7] = _zigCount.ToString();
            state[8] = _zigDir;
            await File.WriteAllLinesAsync("HypState_" + User.Username, state);
        }

        private async Task UpdateCharStats()
        {
            var hist = await HyperionBot.GetLastMessages(10);
            var lastStatsMsg = hist.OrderByDescending(m => m.Date).FirstOrDefault(m => m.Message.Contains("⚡️:"))
                ?.Message;
            if (lastStatsMsg == null)
            {
                await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
                Thread.Sleep(1500);
                lastStatsMsg = (await HyperionBot.GetLastMessage()).Message;
            }

            _hp = short.Parse(lastStatsMsg.Split("❤️: ")[1].Split('/')[0]);
            _x = short.Parse(lastStatsMsg.Split("↕️: ")[1].Substring(0, 3));
            _y = short.Parse(lastStatsMsg.Split("↔️: ")[1].Substring(0, 3));
            _food = short.Parse(lastStatsMsg.Split("🍖: ")[1].Split('/')[0]!);
            _energy = short.Parse(lastStatsMsg.Split("⚡️: ")[1].Split('/')[0]!);
        }

        private string CalculateDirection()
        {
            if (_x < _xTarget && _y < _yTarget)
                return "↗️ СВ";
            if (_x < _xTarget && _y == _yTarget)
                return "⬆️ Север";
            if (_x < _xTarget && _y > _yTarget)
                return "↖️ CЗ";
            if (_x == _xTarget && _y < _yTarget)
                return "➡️ Восток";
            if (_x == _xTarget && _y > _yTarget)
                return "⬅️ Запад";
            if (_x > _xTarget && _y < _yTarget)
                return "↘️ ЮВ";
            if (_x > _xTarget && _y == _yTarget)
                return "⬇️ Юг";
            return "↙️ ЮЗ";
        }

        private async Task DoStepToFarmSpot()
        {
            string direction;

            if (_x == _farmSpot)
            {
                direction = _x == _y ? "⬅️ Запад" : "➡️ Восток";
            }
            else
                direction = "↗️ СВ";
            await DoStep(direction);
        }

        private async Task DoStepToTown()
        {
            var direction = "⬇️ Юг";
            if (_x == _y)
                direction = "↙️ ЮЗ";
            await DoStep(direction);
        }

        private async Task DoStep(string direction)
        {
            var msg = await HyperionBot.GetLastMessage();
            if (msg.Message.Contains("UID:"))
            {
                await HyperionBot.SendMessage("🔙 Назад");
                Thread.Sleep(1500);
                await HyperionBot.SendMessage("👣 Перемещение");
                Thread.Sleep(1500);
            }
            await HyperionBot.SendMessage(direction);
            Thread.Sleep(1000);
            var reply = await HyperionBot.GetLastMessage();
            if (reply.Message == "Дерись!")
            {
                await Fight();
            }
        }

        private async Task Fight()
        {
            if (_x > 10)
            {
                var currentMobDamage = short.Parse(_mobsDamage[_x - 11].Split('-')[1]);
                if (_hp <= 1.5 * currentMobDamage)
                {
                    await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
                    Thread.Sleep(1500);
                    var lastStatsMsg = (await HyperionBot.GetLastMessage()).Message;
                    var mana = short.Parse(lastStatsMsg.Split("🔮: ")[1].Split('/')[0]!);
                    if (mana >= _manaForHeal)
                    {
                        await HyperionBot.SendMessage("⚗️ Эффекты");
                        Thread.Sleep(1500);
                        await HyperionBot.SendMessage("❤️ Исцеление");
                        Thread.Sleep(1500);
                        await HyperionBot.SendMessage("🔙 Назад");
                        Thread.Sleep(1500);
                    }
                }
            }

            await HyperionBot.SendMessage("🔪 В бой");
        }


        private async Task DoRegen()
        {
            await HyperionBot.SendMessage("💤 Отдых");
            Thread.Sleep(1500);

            await HyperionBot.SendMessage("🍗 Еда");
            Thread.Sleep(1500);
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            var foodComIndex = lastMsg.IndexOf($"/buy_{_foodId}_", StringComparison.Ordinal);
            if (foodComIndex > 0)
            {
                var foodOrder = lastMsg.Substring(foodComIndex, 11);
                await HyperionBot.SendMessage(foodOrder);
                Thread.Sleep(1500);
            }

            await HyperionBot.SendMessage("/really_sell_all");
            Thread.Sleep(1500);
            _timeToGoHome = false;
            _outOfFood = false;
        }

        private bool CharInTown()
        {
            return _x == 10 && _y == 10;
        }

        private async Task CheckHp()
        {
            if(_timeToGoHome)
                return;
            var currentHp = _hp;
            if (_x <= 10 || _y <= 10)
                return;

            for (int i = _x; i > 10; i--)
            {
                currentHp -= short.Parse(_mobsDamage[i - 11].Split('-')[1]);
            }

            if (currentHp <= 0)
            {
                if(await TryToHeal())
                    return;
                _timeToGoHome = true;
            }
        }

        private async Task<bool> TryToHeal()
        {
            await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
            Thread.Sleep(1500);

            var lastStatsMsg = (await HyperionBot.GetLastMessage()).Message;
            var mana = short.Parse(lastStatsMsg.Split("🔮: ")[1].Split('/')[0]!);
            if (mana < 2 * _manaForHeal) return false;

            await HyperionBot.SendMessage("❤️ Исцеление");
            Thread.Sleep(1500);
            return true;
        }

        private async Task CheckControls()
        {
            var lastMsg = await SavesChat.GetLastMessage();
            switch (lastMsg.Message)
            {
                case "stop_farm":
                    await SavesChat.SendMessage("Бот остановлен");
                    _disabled = true;
                    _farmInProcess = false;
                    break;
                case "start_farm":
                    await SavesChat.SendMessage("Бот запущен");
                    _disabled = false;
                    _timeToGoHome = false;
                    _outOfFood = false;
                    break;
            }

            if (lastMsg.Message.Contains("set_mob_damage"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                if (parsedString.Length != 3)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: set_mob_damage Х Y\nгде х-лвл моба и у-его урон");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1],out var mobLvl);
                    var secondCheck = short.TryParse(parsedString[2],out var mobDmg);
                    if (firstCheck && secondCheck)
                    {
                        _mobsDamage[mobLvl - 10] = $"{mobLvl}-{mobDmg}";
                        _mobsDamage[mobLvl - 11] = $"{mobLvl+1}-{mobDmg}";
                        await File.WriteAllLinesAsync(Constants.HyperionSettingsFileName + '_' + User.Username,_mobsDamage);
                        await SavesChat.SendMessage($"обновлены мобы {mobLvl} и {mobLvl+1} уровней. Дамаг = {_mobsDamage[mobLvl-10]}");
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_mob_damage Х Y\nгде х-лвл моба и у-его урон");
                    }
                }
            }

            if (lastMsg.Message.Contains("start_zig_move"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                if (parsedString.Length != 3)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: start_zig_move Х Y\nгде х-число ходов и у-[up|down|left|right]");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1],out var zigCount);
                    if (firstCheck)
                    {
                        _zigCount = zigCount;
                        _zigDir = parsedString[2];
                        await SavesChat.SendMessage($"запущен зигзаг {zigCount} ходов в направлении {_zigDir}");
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: start_zig_move Х Y\nгде х-число ходов и у-[up|down|left|right]");
                    }
                }
            }

            if (lastMsg.Message.Contains("set_food_id"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                if (parsedString.Length != 2)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: set_food_id Х");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1],out var foodId);
                    if (firstCheck)
                    {
                        _foodId = foodId;
                        await SavesChat.SendMessage($"задан id еды = {foodId}");
                        await File.WriteAllTextAsync("food_id_" + User.Username, foodId.ToString());
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_food_id Х");
                    }
                }
            }

            if (lastMsg.Message.Contains("set_manaForHeal"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                if (parsedString.Length != 2)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: set_manaForHeal Х");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1],out var mana);
                    if (firstCheck)
                    {
                        _manaForHeal = mana;
                        await SavesChat.SendMessage($"задана мана на хил = {mana}");
                        await File.WriteAllTextAsync("manaForHeal_" + User.Username, mana.ToString());
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_manaForHeal Х");
                    }
                }
            }

            if (lastMsg.Message.Contains("set_farm_spot"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                if (parsedString.Length != 2)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: set_farm_spot Х \nгде х-лвл моба");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1],out var mobLvl);
                    if (firstCheck)
                    {
                        _farmSpot = mobLvl;
                        await SavesChat.SendMessage($"задан уровень фарма = {mobLvl}");
                        await File.WriteAllTextAsync("farm_spot_" + User.Username, _farmSpot.ToString());
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_farm_spot Х \nгде х-лвл моба");
                    }
                }
            }

            if (lastMsg.Message.Contains("move_to"))
            {
                var parsedString = lastMsg.Message.Split(' ');
                var id = (await SavesChat.GetLastMessage()).Id;
                if (parsedString.Length != 3)
                {
                    await SavesChat.SendMessage("неверный формат команды. нужно: move_to Х Y");
                }
                else
                {
                    var firstCheck = short.TryParse(parsedString[1], out var x);
                    var secondCheck = short.TryParse(parsedString[2], out var y);
                    if (firstCheck && secondCheck)
                    {
                        _xTarget = x;
                        _yTarget = y;
                        if (_lastComMesId != id)
                        {
                            _outOfFood = false;
                            _lastComMesId = id;
                        }
                    }
                    else
                    {
                        await SavesChat.SendMessage(
                            "неверный формат команды. нужно: set_mob_damage Х Y\nгде х-лвл моба и у-его урон");
                    }
                }
            }

            if (lastMsg.Message.Contains("show_dmg_list"))
            {
                var textToSend = _mobsDamage.Aggregate("", (current, s) => current + $"{s}\n");
                await SavesChat.SendMessage(textToSend);
            }

            if (lastMsg.Message.Contains("help hyp"))
            {
                var textToSend = "set_mob_damage Х Y где х-лвл моба и у-его урон\nтекущие = show_dmg_list\n";
                textToSend += $"set_food_id Х\nтекущий = {_foodId}\n";
                textToSend += $"set_manaForHeal Х\nтекущий = {_manaForHeal}\n";
                textToSend += $"set_farm_spot Х где х-лвл моба\nтекущий = {_farmSpot}\n";
                textToSend += "move_to Х Y = перейти по координатам\n";
                textToSend += "start_zig_move Х Y\nгде х-число ходов и у-[up|down|left|right]";
                await SavesChat.SendMessage(textToSend);
                await SavesChat.SendMessage("start_farm = включить фарм(нужно быть наа диагонали города(х=у)");
            }
        }
    }
}