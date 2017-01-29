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

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Net;

    /// <summary>
    /// Unit tests of the <see cref="NetUtility"/> class
    /// </summary>
    [TestClass]
    public class NetUtilityUnitTest
    {
        /// <summary>
        /// Determines the public Internet IP address for this node as it would appear to remote nodes in other networks
        /// </summary>
        [TestMethod]
        public void FindPublicIPAddress()
        {
            var foundTask = NetUtility.FindPublicIPAddress();
            if (!foundTask.Wait(10000))
                Assert.Fail("Unable to find a public IP address within 10 seconds.  Is this testing device not able to access the Internet on HTTP/HTTPS ports?");

            Assert.IsNotNull(foundTask.Result, "Unable to find a public IP address.  Is this testing device not able to access the Internet on HTTP/HTTPS ports?");
            Console.WriteLine($"Found IP address: {foundTask.Result}");
        }
    }
}