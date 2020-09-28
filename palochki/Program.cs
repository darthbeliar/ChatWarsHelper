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
                var settingsFile = await File.ReadAllLinesAsync("input");
                var helpers = settingsFile.Select(line => new User(line)).Select(user => new CwHelper(user)).ToList();

                foreach (var cwHelper in helpers)
                {
                    await cwHelper.InitHelper();
                }

                while (true)
                {
                    foreach (var cwHelper in helpers)
                    {
                        await cwHelper.PerformStandardRoutine();
                    }
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