﻿// --------------------------------------------------------------------------------------------------------------------
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
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

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

        private static IPAddress? publicIpAddress;

        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to stop attempting to discover peers</param>
        /// <returns>The public Internet IP address for this node as it would appear to remote nodes in other networks if it could be determined; otherwise, null.</returns>
        internal static async Task<IPAddress?> FindPublicIPAddressAsync(CancellationToken cancellationToken = default)
        {
            if (publicIpAddress != null)
                return publicIpAddress;

            var sources = new[] { @"https://icanhazip.com", @"http://checkip.amazonaws.com", @"http://ipecho.net/plain", @"http://l2.io/ip", @"http://eth0.me", @"http://ifconfig.me/ip" };

            var random = new Random(Environment.TickCount);
            foreach (var source in sources.OrderBy(x => random.Next(1000)))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                using (var wc = new HttpClient())
                {
                    wc.Timeout = new TimeSpan(0, 0, 10); // 10 seconds

                    try
                    {
                        Logger.Debug($"Requesting IP address from remote source {source}");
                        Debug.Assert(source != null, "source != null");
                        var myIp = await wc.GetStringAsync(source);
                        if (string.IsNullOrWhiteSpace(myIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned an empty response");
                            continue;
                        }

                        IPAddress publicIp;
                        if (!IPAddress.TryParse(myIp.Trim()?.TrimEnd('\r', '\n'), out publicIp))
                        {
                            Logger.Warn($"IP lookup source {source} returned a value that could not be parsed into an IP address: {myIp}");
                            continue;
                        }

                        Logger.Info($"External IP address determined to be {publicIp} from remote source {source}");
                        publicIpAddress = publicIp;
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

        public static async Task StartHolePunchRendezvousListener(IPAddress ipAddress, int port = 5109, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

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
                Debug.Assert(stream != null, "stream != null");
                Debug.Assert(stream.CanRead, "stream.CanRead == true");
                using (var ctsRead = new CancellationTokenSource(5000))
                {
                    var bytesRead = new byte[64 * 1024 * 1024];
                    var bytesReadLength = stream.ReadAsync(bytesRead, 0, bytesRead.Length, ctsRead.Token);
                    throw new NotImplementedException();
                }
            }
        }
    }
}