// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileUtilityUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Defines the FileUtilityUnitTest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Cryptography
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Mystiko.Cryptography;

    using Xunit;

    public class EncryptUtilityUnitTest
    {
        [Fact]
        public async Task EncryptDecryptViaFileSystem()
        {
            // Encrypt
            var encryptFilePath = @"C:\Users\Smcelroy\Downloads\node-v6.4.0-x64.msi";
            if (!File.Exists(encryptFilePath))
            {
                Assert.True(false, $"Unable to find test file {encryptFilePath}");
            }
            
            var sourceFile = new FileInfo(encryptFilePath);
            Assert.True(sourceFile.Exists);

            var encryptedSourceFile = new FileInfo(encryptFilePath + ".encrypted-test");
            Assert.False(encryptedSourceFile.Exists);
            
            var encKey = await EncryptUtility.GenerateKeyAndEncryptFileAsync(sourceFile, encryptedSourceFile);
            Assert.NotNull(encKey);
            try
            {

                // Decrypt
                var rebuiltFile = new FileInfo(encryptFilePath + ".rebuilt-test");
                Assert.False(rebuiltFile.Exists);
                await EncryptUtility.DecryptFileAsync(encryptedSourceFile, encKey, rebuiltFile);

                // Compare input encrypt file to output decrypt file
                try
                {
                    var hashOriginal = (await HashUtility.HashFileSHA512Async(sourceFile)).Item1;
                    var hashRebuilt = (await HashUtility.HashFileSHA512Async(rebuiltFile)).Item1;

                    Assert.True(hashOriginal.SequenceEqual(hashRebuilt));
                }
                finally
                {
                    rebuiltFile.Delete();
                }
            }
            finally
            {
                encryptedSourceFile.Delete();
            }
        }
    }
}