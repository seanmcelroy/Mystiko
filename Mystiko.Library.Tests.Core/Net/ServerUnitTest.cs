// --------------------------------------------------------------------------------------------------------------------
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

    using Mystiko.Net;

    using Xunit;

    /// <summary>
    /// Unit tests of the <see cref="Server"/> class
    /// </summary>
    public class ServerUnitTest
    {
        private readonly Tuple<ServerNodeIdentity, byte[]> _serverIdentity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerUnitTest"/> class.
        /// </summary>
        public ServerUnitTest()
        {
            this._serverIdentity = ServerNodeIdentity.Generate(1);
        }

        [Fact]
        public async Task Start()
        {
            using (var server1 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any)))
            {
                Assert.NotNull(server1);
                await server1.StartAsync();

                System.Threading.Thread.Sleep(3000);
            }
        }

        [Fact]
        public async Task ConnectToPeerAsync()
        {
            using (var server1 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any)))
            {
                Assert.NotNull(server1);
                await server1.StartAsync();

                using (var server2 = new Server(false, () => this._serverIdentity, () => new TcpServerChannel(this._serverIdentity.Item1, IPAddress.Any, 5108)))
                {
                    Assert.NotNull(server2);
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
