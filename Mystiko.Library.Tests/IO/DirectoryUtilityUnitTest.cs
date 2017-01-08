namespace Mystiko.Library.Tests.IO
{
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.IO;

    /// <summary>
    /// Summary description for DirectoryUtilityUnitTest
    /// </summary>
    [TestClass]
    public class DirectoryUtilityUnitTest
    {
        [TestMethod]
        public async Task PreHashDirectory()
        {
            // Encrypt
            var localDirectory = @"C:\Users\Smcelroy\Documents\SpiderOak Hive\Pictures\Texas";
            if (!Directory.Exists(localDirectory))
            {
                Assert.Inconclusive($"Unable to find folder {localDirectory}");
            }

            var localManifest = await DirectoryUtility.PreHashDirectory(localDirectory);
            Assert.IsNotNull(localManifest);
        }
    }
}
