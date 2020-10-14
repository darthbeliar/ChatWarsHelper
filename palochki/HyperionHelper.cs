using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TLSharp.Core;

namespace palochki
{
    class HyperionHelper
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

            Console.WriteLine(
                $"\nПользователь {User.Username} подключен к Гипериону\n");
        }

        public async Task DoFarm()
        {
            await CheckControls();
            if (_disabled)
                return;
            if (_waitForStamaRegen)
            {
                await StaminaCheck();
                return;
            }
            var time = DateTime.Now;
            if(time < _pauseStart.AddSeconds(130) || time < _pauseFight.AddSeconds(12))
                return;
            if (!_farmInProcess && !await CharInTown())
            {
                _disabled = true;
                await SavesChat.SendMessage("Для начала фарма нужно быть в городе");
                return;
            }

            _farmInProcess = true;

            if (_inFight)
            {
                _inFight = false;
                if (!await CheckHp())
                    _timeToGoHome = true;
                return;
            }
            if (_timeToGoHome)
            {
                if (await CharInTown())
                    await DoRegen();
                else
                {
                    await DoStepToTown();
                }
            }

            await DoStepToFarmSpot();
        }

        private async Task DoStepToFarmSpot()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            string direction;
            var x = GetX(lastMsg);
            var y = GetY(lastMsg);

            if (x == _farmSpot)
            {
                direction = x == y ? "➡️ Восток" : "⬅️ Запад";
            }
            else
                direction = "↗️ СВ";
            await DoStep(direction,lastMsg);
        }

        private async Task DoStepToTown()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            var direction = "⬅️ Запад";
            if (GetX(lastMsg) == GetY(lastMsg))
                direction = "↙️ ЮЗ";
            await DoStep(direction,lastMsg);
        }

        private async Task DoStep(string direction,string lastMsg)
        {
            if (lastMsg.Contains(Constants.MobHere))
            {
                await HyperionBot.SendMessage("🔪 В бой");
                _inFight = true;
                _pauseFight = DateTime.Now;
                return;
            }
            await HyperionBot.SendMessage(direction);
            Thread.Sleep(1000);
            await StaminaCheck();

            _pauseStart = DateTime.Now;
        }

        private async Task StaminaCheck()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message;
            if (lastMsg == "Ты отдохнул и можешь отправляться дальше")
            {
                _waitForStamaRegen = false;
                await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
                Thread.Sleep(1000);
            }
            if (lastMsg.Contains("Ты слишком устал, чтобы это делать."))
                _waitForStamaRegen = true;
        }

        private async Task DoRegen()
        {
            await HyperionBot.SendMessage("💤 Отдых");
            _timeToGoHome = false;
        }

        private async Task<bool> CheckHp()
        {
            var lastMsg = (await HyperionBot.GetLastMessage()).Message; 
            var currentHp = short.Parse(lastMsg.Split("❤️: ")[1].Substring(0, 2));
            var x = GetX(lastMsg);
            for (int i = x; i > 10; i--)
            {
                currentHp -= short.Parse(_mobsDamage[i - 10].Split('-')[1]);
            }

            return currentHp > 0;
        }

        private async Task<bool> CharInTown()
        {
            await HyperionBot.SendMessage("🏋️‍♂️ Профиль");
            Thread.Sleep(1000);
            var botReply = (await HyperionBot.GetLastMessage()).Message;
            return GetX(botReply) == 10 && GetY(botReply) == 10;
        }

        private static short GetX(string input)
        {
            return short.Parse(input.Split("↕️: ")[1].Substring(0, 2));
        }

        private static short GetY(string input)
        {
            return short.Parse(input.Split("↔️: ")[1].Substring(0, 2));
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
                        await SavesChat.SendMessage($"обновлены мобы {mobLvl} и {mobLvl+1} уровней. Дамаг = {_mobsDamage}");
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_mob_damage Х Y\nгде х-лвл моба и у-его урон");
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
                    }
                    else
                    {
                        await SavesChat.SendMessage("неверный формат команды. нужно: set_farm_spot Х \nгде х-лвл моба");
                    }
                }
            }
        }
    }
}