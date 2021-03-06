﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ServerUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Unit tests of the <see cref="Server" /> class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Net
{
    using System;
    using System.Dynamic;
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net;

    /// <summary>
    /// Unit tests of the <see cref="Server"/> class
    /// </summary>
    [TestClass]
    public class ServerUnitTest
    {
        private Tuple<ServerNodeIdentity, byte[]> _serverIdentity;

        [TestInitialize]
        public void Initialize()
        {
            this._serverIdentity = ServerNodeIdentity.Generate(1);
        }

        [TestMethod]
        public async Task Start()
        {
            using (var server1 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any)))
            {
                Assert.IsNotNull(server1);
                await server1.StartAsync();

                System.Threading.Thread.Sleep(3000);
            }
        }

        [TestMethod]
        public async Task ConnectToPeerAsync()
        {
            using (var server1 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any)))
            {
                Assert.IsNotNull(server1);
                await server1.StartAsync();

                using (var server2 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any, 5108)))
                {
                    Assert.IsNotNull(server2);
                    await server2.StartAsync();

                    dynamic addressInformation = new ExpandoObject();
                    addressInformation.address = IPAddress.Loopback;
                    addressInformation.port = 5108;

                    await server1.ConnectToPeerAsync(addressInformation);
                    System.Threading.Thread.Sleep(3000);
                }
            }
        }
    }
}
