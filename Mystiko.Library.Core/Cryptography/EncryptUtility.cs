namespace Mystiko.Cryptography
{
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    public static class EncryptUtility
    {
        /// <summary>
        /// Generates a single-use key for encrypting a file and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        /// <returns>The encryption key used to encrypt the file</returns>
        public static async Task<byte[]> GenerateKeyAndEncryptFileAsync([NotNull] BufferedStream fileStream, [NotNull] FileInfo destination)
        {
            var encKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(encKey);
            }

            byte[] iv;
            using (var sha = SHA512.Create())
            {
                iv = sha.ComputeHash(encKey).Take(16).ToArray();
            }

            await EncryptFileAsync(fileStream, encKey, iv, destination);

            return encKey;
        }

        /// <summary>
        /// Encrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        public static async Task EncryptFileAsync([NotNull] BufferedStream fileStream, byte[] encKey, byte[] iv, [NotNull] FileInfo destination)
        {
            fileStream.Seek(0, SeekOrigin.Begin);

            using (var aes = Aes.Create())
            {
                Debug.Assert(aes != null, "aes != null");
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;
                using (var encryptor = aes.CreateEncryptor(encKey, iv))
                {
                    using (var msBlock = destination.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var csBlock = new CryptoStream(msBlock, encryptor, CryptoStreamMode.Write))
                    {
                        await fileStream.CopyToAsync(csBlock);
                        csBlock.FlushFinalBlock();
                    }
                }
            }
        }
    }
}
