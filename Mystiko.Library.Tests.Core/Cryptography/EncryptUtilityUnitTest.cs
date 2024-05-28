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
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using Mystiko.Cryptography;
    using Mystiko.Database.Records;

    using Newtonsoft.Json;

    using Xunit;

    public class EncryptUtilityUnitTest
    {
        [Fact]
        public async Task EncryptDecryptViaFileSystem()
        {
            // Encrypt
            var encryptFilePath = @"C:\Users\Sean\Downloads\download.zip";
            if (!File.Exists(encryptFilePath))
            {
                Assert.Fail($"Unable to find test file {encryptFilePath}");
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

        [Fact]
        public async Task EncryptDecryptViaStreams()
        {
            // Setup
            var fakeKey = new byte[32];
            var fakeString = Convert.FromBase64String("asdf");
            Debug.Assert(fakeString != null, "fakeString != null");
            Array.Copy(fakeString, fakeKey, fakeString.Length);

            var configuration = new NodeConfiguration
                                {
                                    Identity = new Mystiko.Net.ServerNodeIdentityAndKey(
                                        Convert.ToUInt64((DateTime.UtcNow - new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds),
                                        fakeKey,
                                        fakeKey,
                                        2,
                                        new byte[32]),
                                    ListenerPort = 1234,
                                    Passive = false,
                                    ResourceRecords = new System.Collections.Generic.List<ResourceRecord>()
                                };

            // Get encryption key
            byte[] encKey;
            using (var pbkdf2Encoder = new Rfc2898DeriveBytes("test", new byte[64], 100000))
            {
                encKey = pbkdf2Encoder.GetBytes(32);
                pbkdf2Encoder.Reset();
            }
            Assert.NotNull(encKey);

            // Encrypt
            string serializedConfigurationOriginal;
            byte[] encryptedConfigBytes;
            using (var fs = new MemoryStream())
            {
                serializedConfigurationOriginal = JsonConvert.SerializeObject(configuration);
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(serializedConfigurationOriginal));
                await EncryptUtility.EncryptStreamAsync(stream, encKey, fs);
                encryptedConfigBytes = fs.ToArray();
            }

            // Decrypt
            {
                var decryptedStream = new MemoryStream();
                await EncryptUtility.DecryptStreamAsync(new MemoryStream(encryptedConfigBytes), encKey, decryptedStream);
                var decryptedBytes = decryptedStream.ToArray();
                Assert.True(decryptedBytes != null, "decryptedBytes.Length != null");
                Assert.True(decryptedBytes.Length > 0, "decryptedBytes.Length > 0");
                var serializedConfigurationRestored = System.Text.Encoding.UTF8.GetString(decryptedBytes).TrimEnd('\0');
                Assert.Equal(serializedConfigurationOriginal, serializedConfigurationRestored);
                var configurationRestored = JsonConvert.DeserializeObject<NodeConfiguration>(serializedConfigurationRestored);
                Assert.NotNull(configurationRestored);
            }
        }
    }
}