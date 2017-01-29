// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NodeAnnounceUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Unit tests for the <see cref="NodeAnnounce" /> class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Net.Messages
{
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net.Messages;

    /// <summary>
    /// Unit tests for the <see cref="NodeAnnounce"/> class
    /// </summary>
    [TestClass]
    public class NodeAnnounceUnitTest
    {
        [TestMethod]
        public void ToWire()
        {
            var na = new NodeAnnounce
            {
                Nodes = new[]
                {
                    new NodeAnnounce.NodeManifest
                        {
                            Address = "0.0.0.0",
                            PublicKeyX = new byte[32],
                            PublicKeyY = new byte[32]
                        },
                    new NodeAnnounce.NodeManifest
                        {
                            Address = "0.0.0.1",
                            PublicKeyX = new byte[32],
                            PublicKeyY = new byte[32]
                        }
                }
            };

            var ret = na.ToPayload();
            Assert.IsNotNull(ret);
        }

        [TestMethod]
        public void FromWire()
        {
            var na = new NodeAnnounce
            {
                Nodes = new[] 
                {
                    new NodeAnnounce.NodeManifest
                        {
                            Address = "0.0.0.0",
                            PublicKeyX = new byte[32],
                            PublicKeyY = new byte[32]
                        },
                    new NodeAnnounce.NodeManifest
                        {
                            Address = "0.0.0.1",
                            PublicKeyX = new byte[32],
                            PublicKeyY = new byte[32]
                        }
                }
            };

            var ret = na.ToPayload();
            Assert.IsNotNull(ret);

            var rebuilt = new NodeAnnounce();
            rebuilt.FromPayload(ret);

            Assert.IsNotNull(rebuilt.Nodes);
            Assert.AreEqual(2, rebuilt.Nodes.Length);
            Assert.IsNotNull(rebuilt.Nodes[0]);
            Assert.AreEqual(na.Nodes[0].Address, rebuilt.Nodes[0].Address);
            Assert.IsTrue(na.Nodes[0].PublicKeyX.SequenceEqual(rebuilt.Nodes[0].PublicKeyX));
            Assert.IsTrue(na.Nodes[0].PublicKeyY.SequenceEqual(rebuilt.Nodes[0].PublicKeyY));
            Assert.IsNotNull(rebuilt.Nodes[1]);
            Assert.AreEqual(na.Nodes[1].Address, rebuilt.Nodes[1].Address);
            Assert.IsTrue(na.Nodes[1].PublicKeyX.SequenceEqual(rebuilt.Nodes[1].PublicKeyX));
            Assert.IsTrue(na.Nodes[1].PublicKeyY.SequenceEqual(rebuilt.Nodes[1].PublicKeyY));
        }
    }
}
