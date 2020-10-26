using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace palochki
{
    internal static class Program
    {
        private static async Task Main()
        {
            var helpersCw = await PrepareHelpersCw();
            var helpersHyp = await PrepareHelpersHyp(helpersCw);
            try
            {
                await MainLoop(helpersCw,helpersHyp);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await MainLoop(helpersCw,helpersHyp);
                throw;
            }
        }

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
                throw;
            }
        }

        private static async Task<List<CwHelper>> PrepareHelpersCw()
        {
            try
            {
                var settingsFile = await File.ReadAllLinesAsync(Constants.InputFileName);
                var helpersCw = settingsFile.Select(line => new User(line)).Select(user => new CwHelper(user)).ToList();
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
        private static async Task MainLoop(List<CwHelper> helpersCw,List<HyperionHelper> helpersHyp)
        {
            try
            {
                while (true)
                {
                    foreach (var cwHelper in helpersCw)
                    {
                        await cwHelper.PerformStandardRoutine();
                    }

                    Thread.Sleep(4000);
                    foreach (var helperHyp in helpersHyp)
                    {
                        await helperHyp.DoFarm();
                    }
                    Thread.Sleep(4000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync(Constants.ErrorLogFileName, $"{DateTime.Now}\n{e.Message}\n");
                Thread.Sleep(30000);
                await MainLoop(helpersCw,helpersHyp);
                throw;
            }
        }
    }
}