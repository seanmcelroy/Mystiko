// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Main entry point for network node server process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Node
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Xml;

    using log4net;
    using log4net.Config;
    using log4net.Repository;
    using log4net.Repository.Hierarchy;

    using Net;

    /// <summary>
    /// Main entry point for network node server process
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point for network node server process
        /// </summary>
        public static void Main()
        {
            Console.WriteLine("Mystiko.Node\r\n");

            // Setup LOG4NET
            var log4netConfig = new XmlDocument();
            using (var reader = new StreamReader(new FileStream("log4net.config", FileMode.Open, FileAccess.Read)))
            {
                log4netConfig.Load(reader);
            }

            var rep = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(Hierarchy));
            XmlConfigurator.Configure(rep, log4netConfig["log4net"]);
            var logger = LogManager.GetLogger(typeof(Program));

            Debug.Assert(logger != null, "logger != null");
            logger.Info("Starting server initialization #1");
            using (var server = new Server())
            {
                Task.Run(async () => { await server.StartAsync(); });
                logger.Info("Server process initialization #1 has completed");

                logger.Info("Starting server initialization #2");
                using (var server2 = new Server(false, listenerPort: 5108))
                {
                    Task.Run(async () => { await server2.StartAsync(); });
                    logger.Info("Server process initialization #2 has completed");
                    Console.ReadLine();
                }
            }
        }
    }
}
