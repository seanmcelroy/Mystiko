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

    using Mystiko.Net.Messages;

    using Xunit;

    /// <summary>
    /// Unit tests for the <see cref="NodeAnnounce"/> class
    /// </summary>
    public class NodeAnnounceUnitTest
    {
        [Fact]
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
            Assert.NotNull(ret);
        }

        [Fact]
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
            Assert.NotNull(ret);

            var rebuilt = new NodeAnnounce();
            rebuilt.FromPayload(ret);

            Assert.NotNull(rebuilt.Nodes);
            Assert.Equal(2, rebuilt.Nodes.Length);
            Assert.NotNull(rebuilt.Nodes[0]);
            Assert.Equal(na.Nodes[0].Address, rebuilt.Nodes[0].Address);
            Assert.True(na.Nodes[0].PublicKeyX.SequenceEqual(rebuilt.Nodes[0].PublicKeyX));
            Assert.True(na.Nodes[0].PublicKeyY.SequenceEqual(rebuilt.Nodes[0].PublicKeyY));
            Assert.NotNull(rebuilt.Nodes[1]);
            Assert.Equal(na.Nodes[1].Address, rebuilt.Nodes[1].Address);
            Assert.True(na.Nodes[1].PublicKeyX.SequenceEqual(rebuilt.Nodes[1].PublicKeyX));
            Assert.True(na.Nodes[1].PublicKeyY.SequenceEqual(rebuilt.Nodes[1].PublicKeyY));
        }
    }
}
