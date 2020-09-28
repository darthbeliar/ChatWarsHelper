﻿namespace palochki
{
    internal class User
    {
        public string Username { get; }
        public int ApiId { get; }
        public string ApiHash { get; }
        public string GuildChatName { get; }
        public string ResultsChatName { get; }
        public string MobsTrigger { get; }

        public User(string rawInput)
        {
            var parsedInput = rawInput.Split('\t');
            Username = parsedInput[0];
            ApiId = int.Parse(parsedInput[1]);
            ApiHash = parsedInput[2];
            GuildChatName = parsedInput[3];
            MobsTrigger = parsedInput[4];
            ResultsChatName = parsedInput[5];
        }
    }
}
