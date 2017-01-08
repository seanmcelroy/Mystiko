namespace Mystiko.Library.Tests
{
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using IO;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.IO;

    [TestClass]
    public class FileUtilityUnitTest
    {
        [TestMethod]
        public async Task EncryptDecryptViaFileSystem()
        {
            // Encrypt
            var encryptFilePath = @"C:\Users\Smcelroy\Downloads\node-v6.4.0-x64.msi";
            if (!File.Exists(encryptFilePath))
            {
                Assert.Inconclusive($"Unable to find test file {encryptFilePath}");
            }
            
            var encryptFile = new FileInfo(encryptFilePath);
            Assert.IsTrue(encryptFile.Exists);

            var chunkResult = await FileUtility.ChunkFileViaOutputDirectory(encryptFile, encryptFile.Directory.FullName, true, true, true);
            Assert.IsNotNull(chunkResult);
            var fileManifest = chunkResult;

            // Decrypt
            var rebuiltFile = new FileInfo(encryptFilePath + ".rebuilt");
            var unchunkResult = await FileUtility.UnchunkFileViaOutputDirectory(fileManifest, encryptFile.DirectoryName, rebuiltFile, true);
            Assert.IsTrue(unchunkResult);

            // Compare input encrypt file to output decrypt file
            using (var sha = SHA512.Create())
            using (var originalStream = encryptFile.OpenRead())
            using (var rebuiltStream = rebuiltFile.OpenRead())
            {
                var firstHash = sha.ComputeHash(originalStream);
                var secondHash = sha.ComputeHash(rebuiltStream);
                
                for (var i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                        Assert.Fail($"SHA512 hash of input file ({firstHash}) did not match rebuilt file ({secondHash}) at position {i} of hash");
                }
            }

            // Clean up
            FileUtility.DeleteChunksInOutputDirectory(fileManifest, encryptFile.DirectoryName);
            rebuiltFile.Delete();
        }
    }
}