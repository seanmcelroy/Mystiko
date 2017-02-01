// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NetUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Utility methods for handling network connectivity
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Net
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    /// <summary>
    /// Utility methods for handling network connectivity
    /// </summary>
    public static class NetUtility
    {
        /// <summary>
        /// The logging implementation for recording the activities that occur in the methods of this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(NetUtility));

        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>The public Internet IP address for this node as it would appear to remote nodes in other networks if it could be determined; otherwise, null.</returns>
        [NotNull, ItemCanBeNull, Pure]
        internal static async Task<IPAddress> FindPublicIPAddressAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var sources = new[] { @"https://icanhazip.com", @"http://checkip.amazonaws.com", @"http://ipecho.net", @"http://l2.io/ip", @"http://eth0.me", @"http://ifconfig.me/ip" };

            foreach (var source in sources)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                using (var wc = new WebClient())
                {
                    try
                    {
                        Logger.Debug($"Requesting IP address from remote source {source}");
                        Debug.Assert(source != null, "source != null");
                        var myIp = await wc.DownloadStringTaskAsync(source);
                        if (string.IsNullOrWhiteSpace(myIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned an empty response");
                            continue;
                        }

                        IPAddress publicIp;
                        if (!IPAddress.TryParse(myIp.Trim().TrimEnd('\r', '\n'), out publicIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned a value that could not be parsed into an IP address: {myIp}");
                            continue;
                        }

                        Logger.Info($"External IP address determined to be {publicIp} from remote source {source}");
                        return publicIp;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Exception when attempting to gather public IP address from {source}", ex);
                    }
                }
            }

            Logger.Warn($"Unable to find public IP address after querying {sources.Length} sources");
            return null;
        }

        public static async Task StartHolePunchRendezvousListener([NotNull] IPAddress ipAddress, int port = 5109, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            var listener = new TcpListener(ipAddress, port)
                               {
                                   ExclusiveAddressUse = false
                               };

            listener.Start();
            while (!cancellationToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                Debug.Assert(tcpClient != null, "tcpClient != null");
                var stream = tcpClient.GetStream();
                using (var ctsRead = new CancellationTokenSource(5000))
                {
                    var bytesRead = new byte[64 * 1024 * 1024];
                    var bytesReadLength = stream.ReadAsync(bytesRead, 0, bytesRead.Length, ctsRead.Token);
                    
                }
            }
        }
    }
}