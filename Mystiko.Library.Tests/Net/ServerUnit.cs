namespace Mystiko.Library.Tests.Net
{
    using System;
    using System.Dynamic;
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net;

    [TestClass]
    public class ServerUnit
    {
        private Tuple<ServerNodeIdentity, byte[]> serverIdentity;


        [TestInitialize]
        public void Initialize()
        {
            this.serverIdentity = ServerNodeIdentity.Generate(1);
        }

        [TestMethod]
        public async Task Start()
        {
            using (var server1 = new Server(() => this.serverIdentity, () => new TcpServerChannel(this.serverIdentity.Item1, IPAddress.Any, 5091)))
            {
                Assert.IsNotNull(server1);
                await server1.StartAsync();

                System.Threading.Thread.Sleep(3000);
            }
        }

        [TestMethod]
        public async Task ConnectToPeerAsync()
        {
            using (var server1 = new Server(() => this.serverIdentity, () => new TcpServerChannel(this.serverIdentity.Item1, IPAddress.Any, 5091)))
            {
                Assert.IsNotNull(server1);
                await server1.StartAsync();

                using (var server2 = new Server(() => this.serverIdentity, () => new TcpServerChannel(this.serverIdentity.Item1, IPAddress.Any, 5092)))
                {
                    Assert.IsNotNull(server2);
                    await server2.StartAsync();

                    dynamic addressInformation = new ExpandoObject();
                    addressInformation.address = IPAddress.Loopback;
                    addressInformation.port = 5092;

                    await server1.ConnectToPeerAsync(addressInformation);
                    System.Threading.Thread.Sleep(3000);
                }
            }
        }
    }
}
