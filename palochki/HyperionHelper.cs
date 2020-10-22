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
        private DateTime _pauseStart;
        private DateTime _pauseFight;
        private bool _inFight;
        private bool _waitForStamaRegen;
        private short _xTarget;
        private short _yTarget;
        private short _foodId;

        public HyperionHelper(User user)
        {
            User = user;
            _disabled = true;
            _farmInProcess = false;
            _timeToGoHome = false;
            _pauseStart = DateTime.MinValue;
            _pauseFight = DateTime.MinValue;
            _inFight = false;
            _farmSpot = 11;
            _waitForStamaRegen = false;
            _xTarget = 100;
            _yTarget = 100;
            _foodId = 101;
        }

        public async Task InitHelper(TelegramClient client)
        {
            if (!User.HyperionUser)
            {
                _disabled = true;
                return;
            }
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

            Console.WriteLine(
                $"\nПользователь {User.Username} подключен к Гипериону\n");
        }

        public async Task DoFarm()
        {
            await CheckControls();

            if (_disabled && _xTarget == 100)
                return;

            if (_waitForStamaRegen)
            {
                await StaminaCheck();
                return;
            }

            var time = DateTime.Now;
            if(time < _pauseStart.AddSeconds(140) || time < _pauseFight.AddSeconds(12))
                return;

            if (_inFight)
            {
                _inFight = false;
                if (!await CheckHp())
                    _timeToGoHome = true;
                return;
            }

            await CheckFood();

            if (_xTarget != 100)
            {
                if (_xTarget == await GetX() && _yTarget == await GetY())
                {
                    _xTarget = 100;
                    await SavesChat.SendMessage("пришли");
                }
                else
                    await DoStep(await CalculateDirection());
                return;
            }

            if (!_farmInProcess && await GetX() != await GetY())
            {
                _disabled = true;
                await SavesChat.SendMessage("Для начала фарма нужно быть в городе");
                return;
            }

            _farmInProcess = true;

            if (_timeToGoHome)
            {
                if (await CharInTown())
                    await DoRegen();
                else
                    await DoStepToTown();
            }
            else
            {
                await DoStepToFarmSpot();
            }
        }

        private async Task CheckFood()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            if (lastMsg.Contains("Похоже ты проголодался"))
            {
                await HyperionBot.SendMessage($"/eat_{_foodId}");
                Thread.Sleep(1500);
                var lastMsgs = await HyperionBot.GetLastMessages(4);
                if (lastMsgs.Any(m => m.Message.Contains(Constants.MobHere)))
                    await Fight();
            }
        }

        private async Task<string> CalculateDirection()
        {
            var x = await GetX();
            var y = await GetY();
            if (x < _xTarget && y < _yTarget)
                return "↗️ СВ";
            if (x < _xTarget && y == _yTarget)
                return "⬆️ Север";
            if (x < _xTarget && y > _yTarget)
                return "↖️ CЗ";
            if (x == _xTarget && y < _yTarget)
                return "➡️ Восток";
            if (x == _xTarget && y > _yTarget)
                return "⬅️ Запад";
            if (x > _xTarget && y < _yTarget)
                return "↘️ ЮВ";
            if (x > _xTarget && y == _yTarget)
                return "⬇️ Юг";
            return "↙️ ЮЗ";
        }

        private async Task DoStepToFarmSpot()
        {
            string direction;
            var x = await GetX();
            var y = await GetY();

            if (x == _farmSpot)
            {
                direction = x == y ? "⬅️ Запад" : "➡️ Восток";
            }
            else
                direction = "↗️ СВ";
            await DoStep(direction);
        }

        private async Task DoStepToTown()
        {
            var direction = "⬇️ Юг";
            if (await GetX() == await GetY())
                direction = "↙️ ЮЗ";
            await DoStep(direction);
        }

        private async Task DoStep(string direction)
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;

            if (lastMsg.Contains(Constants.MobHere))
            {
                await Fight();
                return;
            }

            if (lastMsg.Contains("⚡️: 0"))
            {
                _waitForStamaRegen = true;
                return;
            }

            await HyperionBot.SendMessage(direction);
            Thread.Sleep(1000);
            var reply = await HyperionBot.GetLastMessage();
            if (reply.Message == "Дерись!")
            {
                await Fight();
                return;
            }

            await StaminaCheck();

            _pauseStart = DateTime.Now;
        }

        private async Task Fight()
        {
            await HyperionBot.SendMessage("🔪 В бой");
            _inFight = true;
            _pauseFight = DateTime.Now;
        }

        private async Task StaminaCheck()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            if (lastMsg == "Ты немного отдохнул и можешь продолжать свой путь")
            {
                _waitForStamaRegen = false;
                var preWaitMsgs = await HyperionBot.GetLastMessages(3);
                if (preWaitMsgs.Any(m=>m.Message.Contains("проголодался")))
                {
                    await HyperionBot.SendMessage($"/eat_{_foodId}");
                    Thread.Sleep(1500);
                }
                if (preWaitMsgs.Any(m => m.Message.Contains(Constants.MobHere)))
                    await Fight();
            }

            if (lastMsg.Contains("Ты слишком устал, чтобы это делать."))
                _waitForStamaRegen = true;
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

            _timeToGoHome = false;
        }

        private async Task<bool> CheckHp()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            if (!lastMsg.Contains('❤'))
            {
                await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
                Thread.Sleep(1500);
                lastMsg = (await HyperionBot.GetLastMessage()).Message;
            }
            var currentHp = short.Parse(lastMsg.Split("❤️: ")[1].Split('/')[0]);
            var x = await GetX();
            for (int i = x; i > 10; i--)
            {
                currentHp -= short.Parse(_mobsDamage[i - 11].Split('-')[1]);
            }

            return currentHp > 0;
        }

        private async Task<bool> CharInTown()
        {
            return await GetX() == 10 && await GetY() == 10;
        }

        private async Task<short> GetX()
        {
            var input = (await HyperionBot.GetLastMessage()).Message;
            if (!input.Contains("↕️"))
            {
                await HyperionBot.SendMessage("👣 Перемещение");
                Thread.Sleep(1500);
                input = (await HyperionBot.GetLastMessage()).Message;
            }
            return short.Parse(input.Split("↕️: ")[1].Substring(0, 3));
        }

        private async Task<short> GetY()
        {
            var input = (await HyperionBot.GetLastMessage()).Message;
            if (!input.Contains("↕️"))
            {
                await HyperionBot.SendMessage("👣 Перемещение");
                Thread.Sleep(1500);
                input = (await HyperionBot.GetLastMessage()).Message;
            }
            return short.Parse(input.Split("↔️: ")[1].Substring(0, 3));
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
                textToSend += $"set_farm_spot Х где х-лвл моба\nтекущий = {_farmSpot}\n";
                textToSend += "move_to Х Y = перейти по координатам";
                await SavesChat.SendMessage(textToSend);
                await SavesChat.SendMessage("start_farm = включить фарм(нужно быть наа диагонали города(х=у)");
            }
        }
    }
}