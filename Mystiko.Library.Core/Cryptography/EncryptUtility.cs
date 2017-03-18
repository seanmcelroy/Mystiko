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
        /// <param name="source">The source file to encrypt</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        /// <returns>The encryption key used to encrypt the file</returns>
        [NotNull]
        public static async Task<byte[]> GenerateKeyAndEncryptFileAsync([NotNull] FileInfo source, [NotNull] FileInfo destination)
        {
            var encKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                Debug.Assert(rng != null, "rng != null");
                rng.GetBytes(encKey);
            }

            byte[] iv;
            using (var sha = SHA512.Create())
            {
                Debug.Assert(sha != null, "sha != null");
                iv = sha.ComputeHash(encKey).Take(16).ToArray();
            }

            Debug.Assert(iv != null, "iv != null");
            await EncryptFileAsync(source, encKey, iv, destination);

            return encKey;
        }

        /// <summary>
        /// Generates a single-use key for encrypting a file and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        /// <returns>The encryption key used to encrypt the file</returns>
        [NotNull]
        public static async Task<byte[]> GenerateKeyAndEncryptFileAsync([NotNull] BufferedStream fileStream, [NotNull] FileInfo destination)
        {
            var encKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                Debug.Assert(rng != null, "rng != null");
                rng.GetBytes(encKey);
            }

            byte[] iv;
            using (var sha = SHA512.Create())
            {
                Debug.Assert(sha != null, "sha != null");
                iv = sha.ComputeHash(encKey).Take(16).ToArray();
            }

            Debug.Assert(iv != null, "iv != null");
            await EncryptFileAsync(fileStream, encKey, iv, destination);

            return encKey;
        }

        /// <summary>
        /// Encrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="source">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        [NotNull]
        public static async Task EncryptFileAsync([NotNull] FileInfo source, [NotNull] byte[] encKey, [NotNull] byte[] iv, [NotNull] FileInfo destination)
        {
            using (var bsSource = new BufferedStream(new FileStream(source.FullName, FileMode.Open, FileAccess.Read), 1024 * 1024 * 16))
            using (var msDestination = destination.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Debug.Assert(msDestination != null, "msBlock != null");
                await EncryptStreamAsync(bsSource, encKey, iv, msDestination);
            }
        }

        /// <summary>
        /// Encrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        [NotNull]
        public static async Task EncryptFileAsync([NotNull] Stream fileStream, [NotNull] byte[] encKey, [NotNull] FileInfo destination)
        {
            byte[] iv;
            using (var sha = SHA512.Create())
            {
                Debug.Assert(sha != null, "sha != null");
                iv = sha.ComputeHash(encKey).Take(16).ToArray();
            }

            await EncryptFileAsync(fileStream, encKey, iv, destination);
        }

        /// <summary>
        /// Encrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output encrypted file</param>
        [NotNull]
        public static async Task EncryptFileAsync([NotNull] Stream fileStream, [NotNull] byte[] encKey, [NotNull] byte[] iv, [NotNull] FileInfo destination)
        {
            using (var msBlock = destination.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Debug.Assert(msBlock != null, "msBlock != null");
                await EncryptStreamAsync(fileStream, encKey, iv, msBlock);
            }
        }

        /// <summary>
        /// Encrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="source">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output encrypted stream</param>
        [NotNull]
        public static async Task EncryptStreamAsync([NotNull] Stream source, [NotNull] byte[] encKey, [NotNull] byte[] iv, [NotNull] Stream destination)
        {
            source.Seek(0, SeekOrigin.Begin);

            using (var aes = Aes.Create())
            {
                Debug.Assert(aes != null, "aes != null");
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;
                using (var encryptor = aes.CreateEncryptor(encKey, iv))
                {
                    using (var csBlock = new CryptoStream(destination, encryptor, CryptoStreamMode.Write))
                    {
                        await source.CopyToAsync(csBlock);
                        csBlock.FlushFinalBlock();
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="source">The file to decrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="destination">The destination for the output decrypted file</param>
        [NotNull]
        public static async Task DecryptFileAsync([NotNull] FileInfo source, [NotNull] byte[] encKey, [NotNull] FileInfo destination)
        {
            using (var bsSource = new BufferedStream(new FileStream(source.FullName, FileMode.Open, FileAccess.Read), 1024 * 1024 * 16))
            {
                await DecryptFileAsync(bsSource, encKey, destination);
            }
        }

        /// <summary>
        /// Decrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to decrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="destination">The destination for the output decrypted file</param>
        [NotNull]
        public static async Task DecryptFileAsync([NotNull] Stream fileStream, [NotNull] byte[] encKey, [NotNull] FileInfo destination)
        {
            byte[] iv;
            using (var sha = SHA512.Create())
            {
                Debug.Assert(sha != null, "sha != null");
                iv = sha.ComputeHash(encKey).Take(16).ToArray();
            }

            await DecryptFileAsync(fileStream, encKey, iv, destination);
        }

        /// <summary>
        /// Decrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="fileStream">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output decrypted file</param>
        [NotNull]
        public static async Task DecryptFileAsync([NotNull] Stream fileStream, [NotNull] byte[] encKey, [NotNull] byte[] iv, [NotNull] FileInfo destination)
        {
            using (var msBlock = destination.Open(FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Debug.Assert(msBlock != null, "msBlock != null");
                await DecryptStreamAsync(fileStream, encKey, iv, msBlock);
            }
        }

        /// <summary>
        /// Decrypts a file stream and provides the output to the specified <paramref name="destination"/>
        /// </summary>
        /// <param name="source">The stream to encrypt</param>
        /// <param name="encKey">The encryption key</param>
        /// <param name="iv">The initialization vector</param>
        /// <param name="destination">The destination for the output decrypted stream</param>
        [NotNull]
        public static async Task DecryptStreamAsync([NotNull] Stream source, byte[] encKey, byte[] iv, [NotNull] Stream destination)
        {
            source.Seek(0, SeekOrigin.Begin);

            using (var aes = Aes.Create())
            {
                Debug.Assert(aes != null, "aes != null");
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.Zeros;
                using (var decryptor = aes.CreateDecryptor(encKey, iv))
                {
                    using (var csBlock = new CryptoStream(destination, decryptor, CryptoStreamMode.Write))
                    {
                        await source.CopyToAsync(csBlock);
                        csBlock.FlushFinalBlock();
                    }
                }
            }
        }
    }
}
