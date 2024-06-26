﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IServerChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A channel over which a server accepts connections
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A channel over which a server accepts connections
    /// </summary>
    public interface IServerChannel
    {
        /// <summary>
        /// Gets the clients connected to the server channel
        /// </summary>
        ReadOnlyCollection<IClientChannel> Clients { get; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the server channel will not broadcast its presence, but will listen for other nodes only.
        /// </summary>
        bool Passive { get; set; }

        /// <summary>
        /// Places the server into a state where it listens for new connections
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop listening for new connections and processing existing ones</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        Task StartAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Initiates a connection from this server to a peer node
        /// </summary>
        /// <param name="endpoint">The endpoint information for how to connect to the peer, as interpreted by the <see cref="IServerChannel"/> implementation</param>
        /// <param name="cancellationToken">A cancellation token to stop attempting to connect to the peer</param>
        /// <returns>A task that can be awaited while the operation completes</returns>
        Task<IClientChannel?> ConnectToPeerAsync(IPEndPoint endpoint, CancellationToken cancellationToken);

        /// <summary>
        /// Attempts to discover peers that the server may be able to connect to
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>An enumeration of potential peer IP addresses and ports</returns>
        IEnumerable<Tuple<IPAddress, int>> DiscoverPotentialPeers(CancellationToken cancellationToken);
    }
}
