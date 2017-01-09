namespace Mystiko.Library.Tests.Net
{
    using System.Dynamic;
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net;

    [TestClass]
    public class ServerUnit
    {
        /// <summary>
        /// The server instance for unit testing
        /// </summary>
        private Server server1;

        private Server server2;

        [TestInitialize]
        public void Initialize()
        {
            this.server1 = new Server(() => new TcpServerChannel(IPAddress.Any, 5091));
            this.server2 = new Server(() => new TcpServerChannel(IPAddress.Any, 5092));
        }

        [TestMethod]
        public async Task Start()
        {
            Assert.IsNotNull(this.server1);
            await this.server1.StartAsync();

            System.Threading.Thread.Sleep(3000);
        }

        [TestMethod]
        public async Task ConnectToPeerAsync()
        {
            Assert.IsNotNull(this.server1);
            await this.server1.StartAsync();
            Assert.IsNotNull(this.server2);
            await this.server2.StartAsync();

            dynamic addressInformation = new ExpandoObject();
            addressInformation.address = IPAddress.Loopback;
            addressInformation.port = 5092;

            await this.server1.ConnectToPeerAsync(addressInformation);
            System.Threading.Thread.Sleep(3000);
        }
    }
}
