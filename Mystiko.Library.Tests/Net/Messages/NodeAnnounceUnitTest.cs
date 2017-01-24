namespace Mystiko.Library.Tests.Net.Messages
{
    using System.Linq;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net.Messages;

    [TestClass]
    public class NodeAnnounceUnitTest
    {
        [TestMethod]
        public void ToWire()
        {
            var na = new NodeAnnounce
            {
                Nodes = new[] {
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

            var ret = na.ToWire();
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

            var ret = na.ToWire();
            Assert.IsNotNull(ret);

            var naRebuilt = new NodeAnnounce();
            naRebuilt.FromWire(ret);

            Assert.IsNotNull(naRebuilt.Nodes);
            Assert.AreEqual(2, naRebuilt.Nodes.Length);
            Assert.IsNotNull(naRebuilt.Nodes[0]);
            Assert.AreEqual(na.Nodes[0].Address, naRebuilt.Nodes[0].Address);
            Assert.IsTrue(na.Nodes[0].PublicKeyX.SequenceEqual(naRebuilt.Nodes[0].PublicKeyX));
            Assert.IsTrue(na.Nodes[0].PublicKeyY.SequenceEqual(naRebuilt.Nodes[0].PublicKeyY));
            Assert.IsNotNull(naRebuilt.Nodes[1]);
            Assert.AreEqual(na.Nodes[1].Address, naRebuilt.Nodes[1].Address);
            Assert.IsTrue(na.Nodes[1].PublicKeyX.SequenceEqual(naRebuilt.Nodes[1].PublicKeyX));
            Assert.IsTrue(na.Nodes[1].PublicKeyY.SequenceEqual(naRebuilt.Nodes[1].PublicKeyY));
        }
    }
}
