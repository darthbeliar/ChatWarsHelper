﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace palochki
{
    internal static class Program
    {
        private const int ApiId = 438285; //твой апи прилы с сайта https://my.telegram.org/apps
        private const int ApiId2 = 1846219;
        private const string
            ApiHash = "f8e483e0b7cd38437cf5a9064c43f2cb"; //твой hash прилы с сайта https://my.telegram.org/apps
        private const string
            ApiHash2 = "e3fd2b2f71af0a906ac608b2734fe4ab";

        private const long CwBotAHash = 5368294506206266962;
        private const long CwBotAHash2 = 2382078440132580454;

        private const int TeaId = 1367374268;
        private const long TeaAHash = -2353873925669309700;

        private const int TntId = 1280438334;
        private const long TntaHash = 8925615842454227854;

        private const int ResultsId = 1389695282;
        private const long ResultsAHash = -6679838127471252035;

        private static async Task Main()
        {
            try
            {
                await MainLoop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await MainLoop();
                throw;
            }
        }

        // ReSharper disable once FunctionRecursiveOnAllPaths
        private static async Task MainLoop()
        {
            try
            {
                var trun = new CwHelper("трунь",ApiId,ApiHash,CwBotAHash,TeaId,TeaAHash,"трунь мобы",ResultsId,ResultsAHash);
                await trun.Client.ConnectAsync();
                var beliar = new CwHelper("белиар",ApiId2,ApiHash2,CwBotAHash2,TntId,TntaHash,"белиар мобы");
                await beliar.Client.ConnectAsync();

                while (true)
                {
                    await trun.PerformStandardRoutine();
                    await beliar.PerformStandardRoutine();
                    Thread.Sleep(8000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync("ErrorsLog.txt", $"{DateTime.Now}\n{e.Message}\n");
                await MainLoop();
                throw;
            }
        }
    }
}