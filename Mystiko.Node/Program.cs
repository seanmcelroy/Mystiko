namespace Mystiko.Console
{
    using System;
    using System.Diagnostics;
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

            Debug.Assert(logger != null, "logger != null");
            logger.Info("Starting server initialization #1");
            using (var server = new Server())
            using (var serverTask = Task.Run(async () => { await server.StartAsync(); }))
            {
                logger.Info("Server process initialization #1 has completed");

                logger.Info("Starting server initialization #2");
                using (var server2 = new Server(true, listenerPort: 5108))
                using (var serverTask2 = Task.Run(async () => { await server2.StartAsync(); }))
                {
                    logger.Info("Server process initialization #2 has completed");
                    Console.ReadLine();
                }
            }
        }
    }
}
