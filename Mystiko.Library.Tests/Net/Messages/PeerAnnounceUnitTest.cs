// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PeerAnnounceUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Unit tests for the <see cref="PeerAnnounce" /> class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Net.Messages
{
    using System.Net;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net.Messages;
    
    /// <summary>
    /// Unit tests for the <see cref="PeerAnnounce"/> class
    /// </summary>
    [TestClass]
    public class PeerAnnounceUnitTest
    {
        /// <summary>
        /// Converts the record to a block chain payload
        /// </summary>
        [TestMethod]
        public void ToWire()
        {
            var na = new PeerAnnounce
            {
                PeerNetworkingProtocolVersion = 1,
                PublicIPAddress = IPAddress.Parse("127.0.0.1")
            };

            var ret = na.ToPayload();
            Assert.IsNotNull(ret);
        }

        /// <summary>
        /// Hydrates the record from a block chain payload
        /// </summary>
        [TestMethod]
        public void FromWire()
        {
            var pa = new PeerAnnounce
            {
                PeerNetworkingProtocolVersion = 1,
                PublicIPAddress = IPAddress.Parse("127.0.0.1")
            };

            var ret = pa.ToPayload();
            Assert.IsNotNull(ret);

            var rebuilt = new PeerAnnounce();
            rebuilt.FromPayload(ret);

            Assert.AreEqual(pa.PeerNetworkingProtocolVersion, rebuilt.PeerNetworkingProtocolVersion);
            Assert.IsNotNull(pa.PublicIPAddress);
            Assert.AreEqual(pa.PublicIPAddress, rebuilt.PublicIPAddress);
        }
    }
}
