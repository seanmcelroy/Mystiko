// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileUtilityUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Defines the FileUtilityUnitTest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.IO
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using Mystiko.IO;

    using Xunit;

    public class FileUtilityUnitTest
    {
        [Fact]
        public async Task ChunkFileViaOutputDirectory_NullEncryptFile()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await FileUtility.ChunkFileViaOutputDirectory(null, new DirectoryInfo(@"C:\"), true, true, true));
        }

        [Fact]
        public async Task ChunkFileViaOutputDirectory_MissingEncryptFile()
        {
            var fi = new FileInfo(@"C:\does_not_exist.test");
            if (fi.Exists)
            {
                // XUNIT does not support - Assert.Inconclusive($"Test file actually exists, but should not: {fi.FullName}");
                return;
            }
            else
            {
                await Assert.ThrowsAsync<FileNotFoundException>(async () => await FileUtility.ChunkFileViaOutputDirectory(fi, fi.Directory, true, true, true));
            }
        }

        [Fact]
        public async Task ChunkFileViaOutputDirectory_NullOutputDirectory()
        {
            var encryptFilePath = @"C:\Users\Smcelroy\Downloads\node-v6.4.0-x64.msi";
            if (!File.Exists(encryptFilePath))
            {
                // XUNIT does not support - Assert.Inconclusive($"Unable to find test file {encryptFilePath}");
                return;
            }

            var encryptFile = new FileInfo(encryptFilePath);
            Assert.True(encryptFile.Exists);

            // ReSharper disable once AssignNullToNotNullAttribute
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await FileUtility.ChunkFileViaOutputDirectory(encryptFile, null, true, true, true));
        }

        [Fact(Skip = "Time consuming test")]
        public async Task ChunkFileMetadataOnly_LargeFile()
        {
            // Encrypt
            var largeFilePath = @"C:\Users\Smcelroy\Downloads\en_windows_10_multiple_editions_x64_dvd_6846432.iso";
            if (!File.Exists(largeFilePath))
            {
                // XUNIT does not support - Assert.Inconclusive($"Unable to find test file {largeFilePath}");
                return;
            }

            var encryptFile = new FileInfo(largeFilePath);
            Assert.True(encryptFile.Exists);

            var chunkResult = await FileUtility.ChunkFileMetadataOnly(encryptFile, true);
            Assert.NotNull(chunkResult);
        }

        [Fact]
        public async Task EncryptDecryptViaFileSystem()
        {
            // Encrypt
            var encryptFilePath = @"C:\Users\Smcelroy\Downloads\node-v6.4.0-x64.msi";
            if (!File.Exists(encryptFilePath))
            {
                // XUNIT does not support - Assert.Inconclusive($"Unable to find test file {encryptFilePath}");
                return;
            }
            
            var encryptFile = new FileInfo(encryptFilePath);
            Assert.True(encryptFile.Exists);

            var chunkResult = await FileUtility.ChunkFileViaOutputDirectory(encryptFile, encryptFile.Directory, true, true, true);
            Assert.NotNull(chunkResult);
            var fileManifest = chunkResult;

            // Decrypt
            var rebuiltFile = new FileInfo(encryptFilePath + ".rebuilt");
            var unchunkResult = await FileUtility.UnchunkFileViaOutputDirectory(fileManifest, encryptFile.DirectoryName, rebuiltFile, true, true);
            Assert.True(unchunkResult);

            // Compare input encrypt file to output decrypt file
            using (var sha = SHA512.Create())
            using (var originalStream = encryptFile.OpenRead())
            using (var rebuiltStream = rebuiltFile.OpenRead())
            {
                Debug.Assert(sha != null, "sha != null");
                var firstHash = sha.ComputeHash(originalStream);
                var secondHash = sha.ComputeHash(rebuiltStream);
                
                for (var i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                    {
                        Assert.True(false, $"SHA512 hash of input file ({firstHash}) did not match rebuilt file ({secondHash}) at position {i} of hash");
                    }
                }
            }

            // Clean up
            FileUtility.DeleteChunksInOutputDirectory(fileManifest, encryptFile.DirectoryName);
            rebuiltFile.Delete();
        }
    }
}