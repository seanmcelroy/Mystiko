// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpPeerDiscoveryChannelUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Unit tests of the <see cref="TcpPeerDiscoveryChannel" /> class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Net
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net;

    /// <summary>
    /// Unit tests of the <see cref="TcpPeerDiscoveryChannel"/> class
    /// </summary>
    [TestClass]
    public class TcpPeerDiscoveryChannelUnitTest
    {
        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        [TestMethod]
        public void FindPublicIPAddress()
        {
            var serverIdentity = ServerNodeIdentity.Generate(1);
            Assert.IsNotNull(serverIdentity.Item1, "serverIdentity.Item1 != null");
            var channel = new TcpPeerDiscoveryChannel(serverIdentity.Item1);
            var foundTask = channel.FindPublicIPAddress();
            if (!foundTask.Wait(10000))
                Assert.Fail("Unable to find a public IP address within 10 seconds.  Is this testing device not able to access the Internet on HTTP/HTTPS ports?");

            if (!foundTask.Result)
                Assert.Fail("Unable to find a public IP address.  Is this testing device not able to access the Internet on HTTP/HTTPS ports?");

            Assert.IsNotNull(channel.PublicIPAddress);
            Console.WriteLine($"Found IP address: {channel.PublicIPAddress}");
        }
    }
}
