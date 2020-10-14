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
            var helpersCw = await PrepareClientsCw();
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

        private static async Task<List<CwHelper>> PrepareClientsCw()
        {
            var settingsFile = await File.ReadAllLinesAsync(Constants.InputFileName);
            var helpersCw = settingsFile.Select(line => new User(line)).Select(user => new CwHelper(user)).ToList();
            foreach (var cwHelper in helpersCw)
            {
                await cwHelper.InitHelper();
            }

            return helpersCw;
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

                    Thread.Sleep(8000);
                    foreach (var helperHyp in helpersHyp)
                    {
                        await helperHyp.DoFarm();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync(Constants.ErrorLogFileName, $"{DateTime.Now}\n{e.Message}\n");
                await MainLoop(helpersCw,helpersHyp);
                throw;
            }
        }
    }
}