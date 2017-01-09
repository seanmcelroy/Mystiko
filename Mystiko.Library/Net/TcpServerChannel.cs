// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TcpServerChannel.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A channel for servers to accept TCP/IP clients
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// A channel for servers to accept TCP/IP clients
    /// </summary>
    public class TcpServerChannel : IServerChannel, IDisposable
    {
        [NotNull]
        private readonly TcpListener _listener;

        [NotNull]
        private readonly List<TcpClientChannel> _clients = new List<TcpClientChannel>();

        [CanBeNull]
        private Task _acceptTask;

        private bool _disposed;
        private bool disposing;

        public TcpServerChannel(IPAddress listenAddress = null, int port = 5091)
        {
            this._listener = new TcpListener(listenAddress ?? IPAddress.Any, port);
        }

        /// <inheritdoc />
        public ReadOnlyCollection<IClientChannel> Clients => new ReadOnlyCollection<IClientChannel>(this._clients.Select(c => (IClientChannel)c).ToList());

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            this._acceptTask = new Task(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var tcpClient = await this._listener.AcceptTcpClientAsync();
                    Debug.Assert(tcpClient != null, "tcpClient != null");
                    Console.WriteLine($"Connection from {((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address}:{((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port} to {((IPEndPoint)tcpClient.Client.LocalEndPoint).Address}:{((IPEndPoint)tcpClient.Client.LocalEndPoint).Port}");

                    this._clients.Add(new TcpClientChannel(tcpClient, cancellationToken));
                }
            });

            this._listener.Start();
            this._acceptTask.Start();
        }

        /// <inheritdoc />
        public async Task<IClientChannel> ConnectToPeerAsync(dynamic addressInformation, CancellationToken cancellationToken)
        {
            if (addressInformation == null)
            {
                throw new ArgumentNullException(nameof(addressInformation));
            }

            IPAddress address = addressInformation.address;
            int port = addressInformation.port;

            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);
            return new TcpClientChannel(tcpClient, cancellationToken);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!this._disposed)
            {
                if (this.disposing)
                {
                    // Dispose managed resources.
                    this._listener?.Stop();
                    this._acceptTask?.Dispose();
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }
    }
}
