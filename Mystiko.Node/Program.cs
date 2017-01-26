namespace Mystiko.Console
{
    using System;
    using System.Threading.Tasks;

    using log4net.Config;

    using Net;

    public static class Program
    {
        public static void Main()
        {
            Console.WriteLine("Mystiko.Node\r\n");

            // Setup LOG4NET
            XmlConfigurator.Configure();
            var logger = log4net.LogManager.GetLogger(typeof(Program));

            logger.Info("Starting server initialization");
            using (var server = new Server())
            using (var serverTask = Task.Run(async () => { await server.StartAsync(); }))
            {
                logger.Info("Server process initialization has completed");
                Console.ReadLine();
            }
        }
    }
}
