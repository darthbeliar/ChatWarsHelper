using System.Collections.Generic;

namespace palochki
{
    internal static class Constants
    {
        public const string BotName = "Chat Wars 3";
        public const string ActionLogFile = "action_log_cw";
        public const string HyperionBotName = "Игра Hyperion [Beta]";
        public const string Korovan = "пытается ограбить";
        public const string Stama = "Выносливость восстановлена: ты полон сил";
        public const string InFight = "Ты собрался напасть на врага";
        public const string Village = "/pledge";
        public const string HasMobs = "/fight";
        public const string SuccessArenaStart = "Жажда крови одолела тебя, ты пошел на Арену.";
        public const string HeroCommand = "🏅Герой";
        public const string QuestsCommand = "🗺Квесты";
        public const string RestedState = "🛌Отдых";
        public const string SmithState = "⚒В лавке";
        public const string FastFightCommand = "▶️Быстрый бой";
        public const string StaminaNotFull = "⏰";
        public const string GetReportCommand = "/report";
        public const string BusyState = "Ты сейчас занят другим приключением. Попробуй позже.";
        public const string InputFileName = "input";
        public const string HyperionSettingsFileName = "mobs_damage";
        public const string ErrorLogFileName = "ErrorsLog.txt";
        public const string CatchesLogFileName = "logCathes.txt";
        public const string ReportsHeader = "Твои результаты в бою";
        public const string ForestQuestForRangers = "🌲Лес 3мин. 🔥";
        public const string ForestQuestForRangersN = "🌲Лес 5мин. 🔥";
        public const string SwampQuestForRangers = "🍄Болото 4мин. 🔥";
        public const string SwampQuestForRangersN = "🍄Болото 6мин. 🔥";
        public const int CwBotId = 265204902;
        public static string[] Castles = {"🍁", "🍆", "☘️", "🦇", "🌹", "🖤", "🐢"};
        public static int[] NightHours = {7,8,15,16,23,0};
        public static string[] RagePots = {"p01","p02","p03"};
        public static string[] DefPots = {"p04","p05","p06"};
        public static string[] CwItems =
        {
            "","Thread","Stick","Pelt","Bone","Coal","Charcoal","Powder","Iron ore","Cloth","Silver ore","Bauxite","Cord","Magic stone","Wooden shaft","Sapphire","Solvent","Ruby","Hardener","Steel","Leather","Bone powder","String","Coke","Purified powder","Silver alloy","неведомая хуйня","Steel mold","Silver mold","Blacksmith frame","Artisan frame","Rope","Silver frame","Metal plate","Metallic fiber","Crafted leather","Quality cloth","Blacksmith mold","Artisan mold",
            "Stinky Sumac", "Mercy Sassafras", "Cliff Rue", "Love Creeper", "Wolf Root", "Swamp Lavender", "White Blossom", "Ilaves", "Ephijora", "Storm Hyssop", "Cave Garlic", "Yellow Seed", "Tecceagrass", "Spring Bay Leaf", "Ash Rosemary", "Sanguine Parsley", "Sun Tarragon", "Maccunut", "Dragon Seed", "Queen's Pepper", "Plasma of abyss", "Ultramarine dust", "Ethereal bone", "Itacory", "Assassin Vine", "Kloliarway", "Astrulic", "Flammia Nut", "Plexisop", "Mammoth Dill", 
            "Silver dust", "Suklencia", "Eglalica", "Yoccuran", "Macrider", "Sapphire dust", "Ruby dust", "Teclia shot", "Pygmy spice", "Assagane", "Ripheokam", "Gonitro", "Void Moss"} ;
        public static int[] BattleHours = {0, 8, 16};
        public static int[] AfterBattleHours = {1, 9, 17};
    }
}
