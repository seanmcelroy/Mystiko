using System.Threading.Tasks;

namespace Mystiko.Console
{
    using System;
    using System.IO;
    using System.Linq;

    using Mystiko.IO;

    using Net;

    using File = IO.FileUtility;

    static class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("Starting...");

            using (var server = new Server())
            using (var serverTask = Task.Run(async () => { await server.StartAsync(); }))
            {

                System.Console.ReadLine();
            }


        }
    }
}
