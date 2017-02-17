﻿// --------------------------------------------------------------------------------------------------------------------
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
    using log4net.Repository.Hierarchy;

    using Mystiko.Node.Core;

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
            var log4NetConfig = new XmlDocument();
            using (var reader = new StreamReader(new FileStream("log4net.config", FileMode.Open, FileAccess.Read)))
            {
                log4NetConfig.Load(reader);
            }

            var rep = LogManager.CreateRepository(Assembly.GetEntryAssembly(), typeof(Hierarchy));
            XmlConfigurator.Configure(rep, log4NetConfig["log4net"]);
            var logger = LogManager.GetLogger(typeof(Program));

            Debug.Assert(logger != null, "logger != null");

            using (var node1 = new Node { Tag = "#1" })
            using (var node2 = new Node(listenerPort: 5108) { Tag = "#2" })
            {
                Task.Run(async () => { await node1.StartAsync(); });
                Task.Run(async () => { await node2.StartAsync(); });

                Console.WriteLine("Press ENTER to terminate all nodes");
                Console.ReadLine();
            }
        }
    }
}
