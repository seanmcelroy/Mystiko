﻿// --------------------------------------------------------------------------------------------------------------------
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
                PublicIPAddress = IPAddress.Parse("127.0.0.1"),
                DateEpoch = Convert.ToUInt32((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                PublicKeyX = new byte[32],
                PublicKeyY = new byte[32],
                Nonce = 0
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
                PublicIPAddress = IPAddress.Parse("127.0.0.1"),
                DateEpoch = Convert.ToUInt32((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                PublicKeyX = new byte[32],
                PublicKeyY = new byte[32],
                Nonce = 0
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
