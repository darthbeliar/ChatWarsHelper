#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using palochki.DB_Stuff;

namespace palochki
{
    internal static class Program
    {
        public static PalockiContext Db = new PalockiContext();
        public static List<string> Logs = new List<string>();
        private static async Task Main()
        {
            Console.WriteLine("бот стартанул тип");
            var helpersCw = await PrepareHelpersCw();
            //var helpersHyp = await PrepareHelpersHyp(helpersCw);
            try
            {
                await MainLoop(helpersCw);//,helpersHyp);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await MainLoop(helpersCw); //,helpersHyp);
            }
        }
        /*
        private static async Task<List<HyperionHelper>> PrepareHelpersHyp(List<CwHelper> helpersCw)
        {
            try
            {
                var settingsFile = await File.ReadAllLinesAsync(Constants.InputFileName);
                var helpersHyp = settingsFile.Select(line => new User(line)).Select(user => new HyperionHelper(user))
                    .Where(h => h.User.HyperionUser).ToList();


                foreach (var helperHyp in helpersHyp)
                {
                    var client = helpersCw.FirstOrDefault(h => h.User.Username == helperHyp.User.Username)?.Client;
                    await helperHyp.InitHelper(client);
                }

                return helpersHyp;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(20000);
                return await PrepareHelpersHyp(helpersCw);
            }
        }
        */
        private static async Task<List<CwHelper>> PrepareHelpersCw()
        {
            try
            {
                var helpersCw = new List<CwHelper>();
                foreach (var user in Db.DbUsers)
                {
                    helpersCw.Add(new CwHelper(user));
                }
                foreach (var cwHelper in helpersCw)
                {
                    await cwHelper.InitHelper();
                }

                return helpersCw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Thread.Sleep(30000);
                throw;
            }
        }

        // ReSharper disable once FunctionRecursiveOnAllPaths
        private static async Task MainLoop(IReadOnlyCollection<CwHelper> helpersCw)//,List<HyperionHelper> helpersHyp)
        {
            try
            {
                while (true)
                {
                    for (var i = 0; i < 2; i++)
                    {
                        foreach (var cwHelper in helpersCw)
                        {
                            await cwHelper.PerformFastRoutine();
                            Console.WriteLine($"{DateTime.Now} Fast {i} - {cwHelper.User.UserName} OK");
                        }
                        Thread.Sleep(1000);
                    }

                    foreach (var cwHelper in helpersCw)
                    {
                        await cwHelper.PerformStandardRoutine();
                    }
                    Thread.Sleep(1000);
                }

                //гипера на хуй пока в бане
                /*
                foreach (var helperHyp in helpersHyp)
                {
                    await helperHyp.DoFarm();
                    Console.WriteLine($"{DateTime.Now}: {helperHyp.User.Username}: цикл гипера завершен");
                }
                Thread.Sleep(4000);
                */
                //}
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync(Constants.ErrorLogFileName, $"{DateTime.Now}\n{e.Message}\n");
                var errorHandler = helpersCw.FirstOrDefault(h => h.User.UserName == "трунь");
                await errorHandler.LogChat.SendMessage($"{e.Message}\n\n{e.StackTrace}");
                Thread.Sleep(30000);
                foreach (var cwHelper in helpersCw)
                {
                    await cwHelper.Client.ConnectAsync();
                }

                await MainLoop(helpersCw); //,helpersHyp);
                throw;
            }
        }
    }
}