using System;

namespace Mystiko.Net
{
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    public class Client
    {
        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1048576;

        private readonly TcpClient _client;

        private Task _receiveTask;

        private readonly byte[] _rawInputBuffer = new byte[BufferSize];
        private readonly StringBuilder builder = new StringBuilder();

        internal Client(TcpClient client, CancellationToken serverCancellationToken)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            this._client = client;

            this._receiveTask = new Task(async () =>
            {
                var stream = this._client.GetStream();
                while (true)
                {
                    var bytesRead = await stream.ReadAsync(this._rawInputBuffer, 0, 32767, serverCancellationToken);

                    // There might be more data, so store the data received so far.
                    this.builder.Append(Encoding.UTF8.GetString(this._rawInputBuffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var content = this.builder.ToString();
                    if (bytesRead == BufferSize || !content.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    //Jose.JWT.Decode()

                    var message = content.Split('\r').FirstOrDefault()?.TrimEnd('\r', '\n');
                    Console.WriteLine($"Message received: {message}");

                }
            });
            this._receiveTask.Start();

        }
    }
}
