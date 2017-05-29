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
    using System;
    using System.Net;

    using Mystiko.Net.Messages;

    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="PeerAnnounce"/> class
    /// </summary>
    public class PeerAnnounceUnitTest
    {
        /// <summary>
        /// Converts the record to a block chain payload
        /// </summary>
        [Fact]
        public void ToWire()
        {
            var na = new PeerAnnounce(
                1,
                IPAddress.Loopback,
                5110,
                Convert.ToUInt64((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                new byte[32],
                new byte[32],
                0);

            var ret = na.ToMessage();
            Assert.NotNull(ret);
        }

        /// <summary>
        /// Hydrates the record from a block chain payload
        /// </summary>
        [Fact]
        public void FromWire()
        {
            var pa = new PeerAnnounce(
                1,
                IPAddress.Parse("127.0.0.1"),
                5110,
                Convert.ToUInt64((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                new byte[32],
                new byte[32],
                0);

            var ret = pa.ToMessage();
            Assert.NotNull(ret);

            var rebuilt = new PeerAnnounce(ret);

            Assert.Equal(pa.PeerNetworkingProtocolVersion, rebuilt.PeerNetworkingProtocolVersion);
            Assert.NotNull(pa.PublicIPAddress);
            Assert.Equal(pa.PublicIPAddress, rebuilt.PublicIPAddress);
        }
    }
}
