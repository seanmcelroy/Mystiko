using System.Threading.Tasks;

namespace Mystiko.Console
{
    using System;

    using Net;

    public static class Program
    {
        public static void Main()
        {
            Console.Write("Starting...");

            using (var server = new Server())
            using (var serverTask = Task.Run(async () => { await server.StartAsync(); }))
            {
                Console.WriteLine("UP");
                Console.ReadLine();
            }
        }
    }
}
