// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NetUtilityUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Unit tests of the <see cref="NetUtility" /> class
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Net
{
    using System;

    using Mystiko.Net;

    using Xunit;

    /// <summary>
    /// Unit tests of the <see cref="NetUtility"/> class
    /// </summary>
    public class NetUtilityUnitTest
    {
        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        [Fact]
        public void FindPublicIPAddress()
        {
            var foundTask = NetUtility.FindPublicIPAddressAsync();
            if (!foundTask.Wait(10000))
            {
                Assert.True(false, "Unable to find a public IP address within 10 seconds.  Is this testing device not able to access the Internet on HTTP/HTTPS ports?");
            }

            Assert.NotNull(foundTask.Result);
            Console.WriteLine($"Found IP address: {foundTask.Result}");
        }
    }
}