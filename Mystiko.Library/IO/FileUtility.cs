using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Mystiko.IO
{
    using JetBrains.Annotations;

    public static class FileUtility
    {
        public const uint FILE_PACKAGING_PROTOCOL_VERSION = 1;

        [NotNull]
        public static Tuple<FileManifest, IEnumerable<Block>> ChunkFileViaTemp([NotNull] FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException("File does not exist", file.FullName);

            byte[] fileBytes;
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fileBytes = br.ReadBytes((int)fs.Length);
                br.Close();
            }

            return ChunkFile(fileBytes, file, Block.CreateViaTemp);
        }

        [NotNull]
        public static Tuple<FileManifest, IEnumerable<Block>> ChunkFileViaOutputDirectory([NotNull] FileInfo file, [NotNull] string outputPath, 
            bool overwrite = false,
            bool verbose = false,
            bool verify = false,
            int? chunkSize = null)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException("File does not exist", file.FullName);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            byte[] fileBytes;
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                fileBytes = br.ReadBytes((int)fs.Length);
                br.Close();
            }

            return ChunkFile(fileBytes, file, (ha, ba, ordering) => Block.CreateViaOutputDirectory(ha, ba, outputPath, file.Name, overwrite, verbose, verify), verbose, verify, chunkSize);
        }

        [NotNull]
        private static Tuple<FileManifest, IEnumerable<Block>> ChunkFile([NotNull] byte[] fileBytes, [NotNull] FileInfo fileInfo, [NotNull] Func<HashAlgorithm, byte[], uint, Block> blockCreator, bool verbose = false, bool verify = false, int? chunkSize = null)
        {
            if (fileBytes == null)
                throw new ArgumentNullException(nameof(fileBytes));
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));
            if (blockCreator == null)
                throw new ArgumentNullException(nameof(blockCreator));

            //var length = (int)Math.Ceiling((decimal)fileBytes.Length / Chunk.CHUNK_SIZE_BYTES);
            var chunkLengths = new Dictionary<int, int>();
            var source = new List<Block>();
            var encKey = new byte[32];

            using (var sha = new SHA512Managed())
            {
                if (verbose)
                    Console.Write("Hashing chunks");
                var random = new Random(Environment.TickCount);
                var chunkElapsed = 0;
                var i = 0;

                do
                {
                    //var chunkLength = i == length - 1 ? fileBytes.Length - Chunk.CHUNK_SIZE_BYTES * (length - 1) : Chunk.CHUNK_SIZE_BYTES;
                    var chunkLength = chunkSize ?? random.Next(1024 * 1024, 1024 * 1024 * 10);
                    if (fileBytes.Length - chunkElapsed < chunkLength)
                    {
                        // Last chunk
                        chunkLength = fileBytes.Length - chunkElapsed;
                    }
                    else
                    {
                        if (chunkLength % 128 != 0)
                            chunkLength -= chunkLength % 128;
                    }
                    
                    var chunkBuffer = new byte[chunkLength];
                    Array.Copy(fileBytes, chunkElapsed, chunkBuffer, 0, chunkLength);
                    var chunkHash = sha.ComputeHash(chunkBuffer, 0, chunkLength).Take(32).ToArray();
                    if (verbose)
                        Console.Write(".");
                    encKey = ExclusiveOR(encKey, chunkHash);
                    chunkElapsed += chunkLength;
                    chunkLengths.Add(i, chunkLength);
                    i++;
                }
                while (chunkElapsed < fileBytes.Length);

                if (verbose)
                    Console.WriteLine($"\r\nEncryption Key: {ByteArrayToString(encKey)}");
                using (var aes = new AesManaged
                {
                    KeySize = 256,
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.Zeros
                })
                {
                    // Setup IV
                    var iv = sha.ComputeHash(encKey).Take(16).ToArray();
                    using (var encryptor = aes.CreateEncryptor(encKey, iv))
                    {
                        if (verbose)
                            Console.WriteLine("Encrypting blocks");

                        var blockElapsed = 0;
                        foreach (var blockLength in chunkLengths)
                        {
                            using (var msBlock = new MemoryStream(blockLength.Value))
                            using (var csBlock = new CryptoStream(msBlock, encryptor, CryptoStreamMode.Write))
                            {
                                csBlock.Write(fileBytes, blockElapsed, blockLength.Value);
                                if (blockLength.Key == chunkLengths.Count - 1)
                                    csBlock.FlushFinalBlock();

                                var encryptedBlockBytes = new byte[blockLength.Value + Math.Max(0L, msBlock.Length % 128L - blockLength.Value % 128)];
                                msBlock.ToArray().CopyTo(encryptedBlockBytes, 0);
                                var block = blockCreator(sha, encryptedBlockBytes, Convert.ToUInt32(blockLength.Key));
                                source.Add(block);

                                if (verbose)
                                {
                                    Console.WriteLine($" {blockLength.Key} {Path.GetFileName(block.Path)} ({new FileInfo(block.Path).Length:N0} bytes)");
                                    Console.WriteLine($" \\-{ByteArrayToString(block.FullHash)}");
                                }
                                csBlock.Close();
                            }
                            blockElapsed += blockLength.Value;
                        }
                    }
                }
            }

            var unlockXorKey = Block.CalculateUnlockXorKey(encKey, source);
            if (verbose)
                Console.WriteLine($"Finalized manifest unlock XorKey: {ByteArrayToString(unlockXorKey)}");
            var recoveredEncKey = ExclusiveOR(source.Select(b => b.FullHash.Take(32).ToArray()).Aggregate(ExclusiveOR), unlockXorKey);
            Debug.Assert(encKey.SequenceEqual(recoveredEncKey));

            return new Tuple<FileManifest, IEnumerable<Block>>(new FileManifest
            {
                Blocks = source,
                File = fileInfo,
                UnlockBytes = unlockXorKey
            }, source);
        }

        public static bool UnchunkFileViaOutputDirectory([NotNull] FileInfo manifestFile, [NotNull] FileInfo saveFile, bool overwrite)
        {
            if (manifestFile == null)
                throw new ArgumentNullException(nameof(manifestFile));
            if (saveFile == null)
                throw new ArgumentNullException(nameof(saveFile));

            FileManifest manifest;
            using (var fs = new FileStream(manifestFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Write))
            using (var sr = new StreamReader(fs))
            {
                manifest = JsonConvert.DeserializeObject<FileManifest>(sr.ReadToEnd());
                fs.Close();
            }

            var fileInfoList = new List<FileInfo>();
            foreach (var file1 in Directory.GetFiles(manifestFile.Directory.FullName))
            {
                var file = file1;
                if (manifest.BlockHashes.Any(bh => file.IndexOf($"{manifest.Name}.{bh.Substring(0, 8)}", StringComparison.OrdinalIgnoreCase) > -1))
                    fileInfoList.Add(new FileInfo(file));
            }
            return UnchunkFile(manifest, fileInfoList, saveFile, overwrite);
        }

        public static bool UnchunkFile([NotNull] FileManifest manifest, [NotNull] ICollection<FileInfo> fileBlocks, [NotNull] FileInfo saveFile, bool overwrite = false, bool verbose = false, bool verify = false)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (fileBlocks == null)
                throw new ArgumentNullException(nameof(fileBlocks));
            if (saveFile == null)
                throw new ArgumentNullException(nameof(saveFile));

            if (saveFile.Exists)
            {
                if (!overwrite)
                    return false;
                saveFile.Delete();
            }

            var source = new List<Block>();
            var dictionary = new Dictionary<FileInfo, Block>();
            using (var sha = new SHA512Managed())
            {
                foreach (var fileBlock in fileBlocks)
                    using (var fs = new FileStream(fileBlock.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        var hash = sha.ComputeHash(fs);
                        if (verbose)
                            Console.WriteLine($"Hashed block for manifest matching: {fileBlock.Name} ({fileBlock.Length:N0} bytes) -> {ByteArrayToString(hash)}");

                        fs.Seek(-32, SeekOrigin.End);
                        var last32Bytes = new byte[32];
                        fs.Read(last32Bytes, 0, 32);

                        dictionary.Add(fileBlock, new Block(fileBlock.FullName, hash, last32Bytes));
                        fs.Close();
                    }
            }

            var i = 0;
            foreach (var blockHash in manifest.BlockHashes)
            {
                var flag = false;
                var byteArray = StringToByteArray(blockHash);
                foreach (var keyValuePair in dictionary)
                {
                    if (keyValuePair.Value.FullHash.SequenceEqual(byteArray))
                    {
                        keyValuePair.Value.Ordering = i;
                        source.Add(keyValuePair.Value);
                        ++i;
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                    Console.WriteLine($"Could not match manifest block hash: {blockHash}");
            }

            var encKey = ExclusiveOR(source.Select(b => b.FullHash.Take(32).ToArray()).Aggregate(ExclusiveOR), StringToByteArray(manifest.Unlock));
            if (verbose)
                Console.WriteLine($"Recovered Encryption Key: {ByteArrayToString(encKey)}");

            // Setup IV
            byte[] iv;
            using (var sha = new SHA512Managed())
                iv = sha.ComputeHash(encKey).Take(16).ToArray();

            using (var fsSave = new FileStream(saveFile.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var bwSave = new BinaryWriter(fsSave))
            {
                foreach (var block in source)
                {
                    using (var aes = new AesManaged
                                     {
                                         KeySize = 256,
                                         Mode = CipherMode.CBC,
                                         Padding = PaddingMode.Zeros
                                     })
                    {
                        using (var decryptor = aes.CreateDecryptor(encKey, iv))
                        {
                            using (var fsBlock = new FileStream(block.Path, FileMode.Open, FileAccess.Read, FileShare.None))
                            using (var csBlock = new CryptoStream(fsBlock, decryptor, CryptoStreamMode.Read))
                            {
                                if (verbose)
                                    Console.Write($"\r\nDecrypting Block {block.Ordering}: {Path.GetFileName(block.Path)}");
                                var buffer = new byte[aes.BlockSize * 1024];
                                var totalDecryptedRead = 0;
                                int decryptReadActual;
                                do
                                {
                                    var decryptReadMax = Math.Min(buffer.Length, (int)fsBlock.Length - totalDecryptedRead);
                                    if (decryptReadMax != 0)
                                    {
                                        if ((uint)(decryptReadMax % 128) > 0U)
                                            decryptReadMax += 128 - decryptReadMax % 128;
                                        if ((int)fsBlock.Length - totalDecryptedRead < buffer.Length)
                                            decryptReadMax -= 4;
                                        decryptReadActual = csBlock.Read(buffer, 0, decryptReadMax);
                                        totalDecryptedRead += decryptReadActual;
                                        if (verbose)
                                            Console.Write(".");
                                        bwSave.Write(buffer, 0, decryptReadActual);
                                    }
                                    else
                                        break;
                                }
                                while (decryptReadActual > 0);
                                csBlock.Close();
                            }
                        }
                    }
                    if (verbose)
                        Console.WriteLine();
                }
                fsSave.Close();
            }
            return true;
        }

        [NotNull, Pure]
        public static byte[] ExclusiveOR([NotNull] byte[] arr1, [NotNull] byte[] arr2)
        {
            if (arr1 == null)
                throw new ArgumentNullException(nameof(arr1));
            if (arr2 == null)
                throw new ArgumentNullException(nameof(arr2));
            if (arr1.Length != arr2.Length)
                throw new ArgumentException("arr1 and arr2 are not the same length");
            var numArray = new byte[arr1.Length];
            for (var index = 0; index < arr1.Length; ++index)
                numArray[index] = (byte)(arr1[index] ^ (uint)arr2[index]);
            return numArray;
        }

        [NotNull, Pure]
        public static byte[] StringToByteArray([CanBeNull] string hex)
        {
            return hex == null ? new byte[0] : Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
        }

        [CanBeNull, Pure]
        public static string ByteArrayToString([CanBeNull] byte[] bytes)
        {
            if (bytes == null)
                return null;
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
    }
}
