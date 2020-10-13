using System;
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
                var settingsFile = await File.ReadAllLinesAsync(Constants.InputFileName);
                var helpersCw = settingsFile.Select(line => new User(line)).Select(user => new CwHelper(user)).ToList();
                var helpersHyp = settingsFile.Select(line => new User(line)).Select(user => new HyperionHelper(user))
                    .Where(h => h.User.HyperionUser).ToList();

                foreach (var cwHelper in helpersCw)
                {
                    await cwHelper.InitHelper();
                }

                foreach (var helperHyp in helpersHyp)
                {
                    var client = helpersCw.FirstOrDefault(h => h.User.Username == helperHyp.User.Username).Client;
                    await helperHyp.InitHelper(client);
                }

                while (true)
                {
                    foreach (var cwHelper in helpersCw)
                    {
                        await cwHelper.PerformStandardRoutine();
                    }

                    foreach (var helperHyp in helpersHyp)
                    {
                        await helperHyp.DoFarm();
                    }

                    Thread.Sleep(8000);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await File.AppendAllTextAsync(Constants.ErrorLogFileName, $"{DateTime.Now}\n{e.Message}\n");
                await MainLoop();
                throw;
            }
        }
    }
}