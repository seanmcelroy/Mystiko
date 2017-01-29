// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Main entry point for network node server process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Console
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    using log4net.Config;

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
            XmlConfigurator.Configure();
            var logger = log4net.LogManager.GetLogger(typeof(Program));

            Debug.Assert(logger != null, "logger != null");
            logger.Info("Starting server initialization #1");
            using (var server = new Server())
            using (var serverTask = Task.Run(async () => { await server.StartAsync(); }))
            {
                logger.Info("Server process initialization #1 has completed");

                logger.Info("Starting server initialization #2");
                using (var server2 = new Server(false, listenerPort: 5108))
                using (var serverTask2 = Task.Run(async () => { await server2.StartAsync(); }))
                {
                    logger.Info("Server process initialization #2 has completed");
                    Console.ReadLine();
                }
            }
        }
    }
}
