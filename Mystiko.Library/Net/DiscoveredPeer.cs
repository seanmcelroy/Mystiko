// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DiscoveredPeer.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A peer that was discovered by the <see cref="TcpPeerDiscoveryChannel" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Net;

    using JetBrains.Annotations;

    /// <summary>
    /// A peer that was discovered by the <see cref="TcpPeerDiscoveryChannel"/>
    /// </summary>
    internal sealed class DiscoveredPeer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DiscoveredPeer"/> class.
        /// </summary>
        /// <param name="nodeIdentity">
        /// The identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
        /// meet a target difficulty requirement
        /// </param>
        /// <param name="discoveryEndpoint">
        /// The network remote endpoint through which this node was discovered
        /// </param>
        public DiscoveredPeer([NotNull] ServerNodeIdentity nodeIdentity, [NotNull] IPEndPoint discoveryEndpoint)
        {
            if (nodeIdentity == null)
            {
                throw new ArgumentNullException(nameof(nodeIdentity));
            }

            if (discoveryEndpoint == null)
            {
                throw new ArgumentNullException(nameof(discoveryEndpoint));
            }

            this.NodeIdentity = nodeIdentity;
            this.DiscoveryEndpoint = discoveryEndpoint;
        }

        /// <summary>
        /// Gets the identity of a node consisting of a date of generation, key pair, and nonce proving the date and public key
        /// meet a target difficulty requirement
        /// </summary>
        [NotNull]
        public ServerNodeIdentity NodeIdentity { get; private set; }

        /// <summary>
        /// Gets the network remote endpoint through which this node was discovered
        /// </summary>
        [NotNull]
        public IPEndPoint DiscoveryEndpoint { get; private set; }
    }
}
