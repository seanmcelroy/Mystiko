using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mystiko.Net
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    public class Server : IDisposable
    {
        private TcpListener _listener;

        private readonly List<Client> _clients = new List<Client>();

        private Task _acceptTask;

        private readonly CancellationTokenSource _acceptCancellationTokenSource = new CancellationTokenSource();

        private bool _disposed;
        private bool disposing;

        public async Task StartAsync()
        {
            this._listener = new TcpListener(IPAddress.Any, 5091);
            this._acceptTask = new Task(async () =>
            {
                while (!this._acceptCancellationTokenSource.Token.IsCancellationRequested)
                {
                    var tcpClient = await this._listener.AcceptTcpClientAsync();
                    this._clients.Add(new Client(tcpClient, this._acceptCancellationTokenSource.Token));
                }
            });

            this._listener.Start();
            this._acceptTask.Start();
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
                    this._acceptCancellationTokenSource.Cancel();
                    this._listener.Stop();
                    this._acceptTask.Dispose();
                }

                // There are no unmanaged resources to release, but
                // if we add them, they need to be released here.
            }

            this._disposed = true;
        }
    }
}
