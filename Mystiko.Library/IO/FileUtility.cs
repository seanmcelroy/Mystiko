﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FileUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Utility methods for handling the hashing, encryption, and splitting of files
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.IO
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Newtonsoft.Json;

    /// <summary>
    /// Utility methods for handling the hashing, encryption, and splitting of files
    /// </summary>
    public static class FileUtility
    {
        /// <summary>
        /// The version of the file share management protocol
        /// </summary>
        public const uint FILE_LOCAL_SHARE_MANAGEMENT_PROTOCOL_VERSION = 1;

        /// <summary>
        /// The version of the file packaging protocol
        /// </summary>
        public const uint FILE_PACKAGING_PROTOCOL_VERSION = 1;

        [NotNull, ItemNotNull, Pure]
        public static async Task<FileManifest> ChunkFileMetadataOnly(
            [NotNull] FileInfo file,
            bool verbose = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException("File does not exist", file.FullName);
            
            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var bs = new BufferedStream(fs, 1024 * 1024 * 16))
            {
                try
                {
                    Console.WriteLine();
                    var result = await ChunkFile(
                        bs, 
                        file, 
                        Block.NoSavedChunk, 
                        verbose: verbose,
                        chunkFileAction: chunkIndex =>
                            {
                                if (verbose)
                                {
                                    Console.Write(".");
                                }
                            },
                        cancellationToken: cancellationToken);
                        return result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to chunk file '{file.FullName}'", ex);
                }
            }
        }

        [NotNull]
        public static async Task<FileManifest> ChunkFileViaTemp([NotNull] FileInfo file, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (!file.Exists)
            {
                throw new FileNotFoundException("File does not exist", file.FullName);
            }

            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var bs = new BufferedStream(fs))
            {
                return await ChunkFile(bs, file, Block.CreateViaTemp, null, false, cancellationToken: cancellationToken);
            }
        }

        [NotNull, ItemNotNull]
        public static async Task<FileManifest> ChunkFileViaOutputDirectory(
            [NotNull] FileInfo file, 
            [NotNull] string outputPath, 
            bool overwrite = false,
            bool verbose = false,
            bool verify = false,
            int? chunkSize = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            if (!file.Exists)
                throw new FileNotFoundException("File does not exist", file.FullName);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            using (var bs = new BufferedStream(fs, 1024 * 1024 * 16))
            {
                return await ChunkFile(
                    bs, 
                    file, 
                    async (ha, ba, ordering) =>
                    {
                        Debug.Assert(ha != null, "ha != null");
                        Debug.Assert(ba != null, "ba != null");
                        return await Block.CreateViaOutputDirectory(ha, ba, outputPath, file.Name, overwrite, verbose, verify);
                    }, 
                    verbose: verbose, 
                    chunkSize: chunkSize,
                    chunkFileAction: chunkIndex =>
                    {
                        if (verbose)
                        {
                            Console.Write(".");
                        }
                    },
                    cancellationToken: cancellationToken);
            }
        }

        [NotNull, ItemCanBeNull]
        public static async Task<FileManifest> ChunkFileViaOutputDirectoryFromPreHash(
            [NotNull] FileInfo sourceFile,
            [NotNull] FileInfo manifestFile,
            [NotNull] string outputPath,
            bool overwrite = false,
            bool verbose = false,
            bool verify = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (sourceFile == null)
                throw new ArgumentNullException(nameof(sourceFile));
            if (!sourceFile.Exists)
                throw new FileNotFoundException("Source file does not exist", sourceFile.FullName);
            if (manifestFile == null)
                throw new ArgumentNullException(nameof(manifestFile));
            if (!manifestFile.Exists)
                throw new FileNotFoundException("Manifest file does not exist", manifestFile.FullName);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentNullException(nameof(outputPath));

            using (var fs = new FileStream(sourceFile.FullName, FileMode.Open, FileAccess.Read))
            using (var bs = new BufferedStream(fs, 1024 * 1024 * 16))
            {
                return await ChunkFile(
                    bs,
                    manifestFile,
                    async (ha, ba, ordering) =>
                    {
                        Debug.Assert(ha != null, "ha != null");
                        Debug.Assert(ba != null, "ba != null");
                        return await Block.CreateViaOutputDirectory(ha, ba, outputPath, sourceFile.Name, overwrite, verbose, verify);
                    },
                    verbose,
                    cancellationToken);
            }
        }

        [Pure, NotNull]
        public static IEnumerable<int> GetChunkLengths(long fileSizeBytes, int? chunkSize)
        {
            var random = new Random(Environment.TickCount);
            var chunkElapsed = 0L;

            do
            {
                //var chunkLength = i == length - 1 ? fileBytes.Length - Chunk.CHUNK_SIZE_BYTES * (length - 1) : Chunk.CHUNK_SIZE_BYTES;

                /* We need to make sure huge files can be uploaded; but that may mean a lot of chunks
                 * would mean thousands if not millions of chunks with a small size.
                 * Chunks are at least 1 MB (1024x1024), but for larger files, we need to scale this so chunks are larger
                 */

                var chunkLengthMinimum = Math.Max(1024 * 1024, (int)Math.Pow(10, Math.Floor(Math.Log10(fileSizeBytes)) - 2));
                var chunkLengthMaximum = Math.Max(1024 * 1024 * 10, (int)Math.Pow(10, Math.Floor(Math.Log10(fileSizeBytes)) - 1));

                var chunkLength = chunkSize ?? random.Next(chunkLengthMinimum, chunkLengthMaximum);
                if (fileSizeBytes - chunkElapsed < chunkLength)
                {
                    // Last chunk
                    chunkLength = (int)(fileSizeBytes - chunkElapsed);
                }
                else
                {
                    if (chunkLength % 128 != 0)
                        chunkLength -= chunkLength % 128;
                }

                chunkElapsed += chunkLength;

                yield return chunkLength;
            }
            while (chunkElapsed < fileSizeBytes);
        }

        [NotNull, ItemNotNull]
        private static async Task<FileManifest> ChunkFile(
            [NotNull] BufferedStream fileStream,
            [NotNull] FileInfo fileInfo,
            [NotNull] Func<HashAlgorithm, byte[], uint, Task<Block>> blockCreatorTask,
            int? chunkSize = null,
            bool verbose = false,
            Action<int> chunkFileAction = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));
            if (!fileStream.CanRead)
                throw new InvalidOperationException("File stream does not support reads in its current state");
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));
            if (blockCreatorTask == null)
                throw new ArgumentNullException(nameof(blockCreatorTask));

            //var length = (int)Math.Ceiling((decimal)fileBytes.Length / Chunk.CHUNK_SIZE_BYTES);
            var chunkLengths = new ConcurrentDictionary<int, int>();
            var source = new List<Block>();
            var encKey = new byte[32];

                if (verbose)
                    Console.Write("Hashing chunks");

                // Producer-consumer queue for hashing chunks of the file
                var dataItems = new BlockingCollection<Tuple<int, byte[]>>(4);

                var producerTask = Task.Run(async () =>
                    {
                        var i = 0;

                        foreach (var chunkLength in GetChunkLengths(fileStream.Length, chunkSize))
                        {
                            var chunkBuffer = new byte[chunkLength];
                            await fileStream.ReadAsync(chunkBuffer, 0, chunkLength, cancellationToken);
                            dataItems.Add(new Tuple<int, byte[]>(i, chunkBuffer), cancellationToken);
                            if (verbose)
                                Console.Write("_");

                            while (!chunkLengths.TryAdd(i, chunkLength))
                            {
                            }

                            i++;
                        }

                        // Signal we are done
                        dataItems.CompleteAdding();
                    },
                    cancellationToken);

                var chunkHashes = new ConcurrentDictionary<int, byte[]>();

                var consumerFunction = new Action(
                    () =>
                        {
                            using (var sha = new SHA512Managed())
                            {
                                while (!dataItems.IsCompleted)
                                {
                                    Tuple<int, byte[]> dataItem;
                                    try
                                    {
                                        dataItem = dataItems.Take(cancellationToken);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        // Queue empty.
                                        Console.WriteLine("Queue empty!");
                                        return;
                                    }

                                    Debug.Assert(dataItem != null, "dataItem != null");
                                    var i = dataItem.Item1;
                                    var chunkBuffer = dataItem.Item2;
                                    var chunkHash = sha.ComputeHash(chunkBuffer, 0, chunkBuffer.Length).Take(32).ToArray();

                                    while (!chunkHashes.TryAdd(i, chunkHash))
                                    {
                                        if (verbose)
                                        {
                                            Console.WriteLine("Unable to add chunk...");
                                        }
                                    }

                                    chunkFileAction?.Invoke(i);
                                }
                            }
                        });

                Task.WaitAll(
                    producerTask, 
                    Task.Run(consumerFunction, cancellationToken),
                    Task.Run(consumerFunction, cancellationToken),
                    Task.Run(consumerFunction, cancellationToken),
                    Task.Run(consumerFunction, cancellationToken));

                foreach (var chunkHash in chunkHashes.Values)
                {
                    Debug.Assert(chunkHash != null, "chunkHash != null");
                    encKey = ExclusiveOr(encKey, chunkHash);
                }

                if (verbose)
                    Console.WriteLine($"\r\nEncryption Key: {ByteArrayToString(encKey)}");

                fileStream.Seek(0, SeekOrigin.Begin);
                using (var aes = new AesManaged
                {
                    KeySize = 256,
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.Zeros
                })
                {
                // Setup IV
                    using (var sha = new SHA512Managed())
                    {
                        var iv = sha.ComputeHash(encKey).Take(16).ToArray();
                        using (var encryptor = aes.CreateEncryptor(encKey, iv))
                        {
                            if (verbose)
                                Console.WriteLine("Encrypting blocks");

                            var i = 0;
                            foreach (var blockLength in chunkLengths)
                            {
                                if (verbose)
                                    Console.WriteLine($"Encrypting block {i + 1} of {chunkLengths.Count} ({(double)i / chunkLengths.Count:P0})");
                                i++;

                                var fileBytes = new byte[blockLength.Value];
                                await fileStream.ReadAsync(fileBytes, 0, blockLength.Value);

                                using (var msBlock = new MemoryStream(blockLength.Value))
                                using (var csBlock = new CryptoStream(msBlock, encryptor, CryptoStreamMode.Write))
                                {
                                    csBlock.Write(fileBytes, 0, fileBytes.Length);
                                    byte[] encryptedBlockBytes;
                                    if (blockLength.Key == chunkLengths.Count - 1)
                                    {
                                        csBlock.FlushFinalBlock();
                                        encryptedBlockBytes = new byte[msBlock.Length];
                                    }
                                    else
                                    {
                                        encryptedBlockBytes = new byte[blockLength.Value + Math.Max(0L, msBlock.Length % 128L - blockLength.Value % 128)];
                                    }

                                    msBlock.ToArray().CopyTo(encryptedBlockBytes, 0);
                                    var block = await blockCreatorTask.Invoke(sha, encryptedBlockBytes, Convert.ToUInt32(blockLength.Key));
                                    if (block == null)
                                        throw new InvalidOperationException("Block creator returned 'null'");

                                    source.Add(block);

                                    if (verbose)
                                    {
                                        Console.WriteLine(block.Path == null ? $" {blockLength.Key} {Path.GetFileName(block.Path)} ({encryptedBlockBytes.Length:N0} bytes)" : $" {blockLength.Key} {Path.GetFileName(block.Path)} ({new FileInfo(block.Path).Length:N0} bytes)");
                                        Console.WriteLine($" \\-{ByteArrayToString(block.FullHash)}");
                                    }
                                    csBlock.Close();
                                }
                            }
                        }
                    }
                }

            var unlockXorKey = Block.CalculateUnlockXorKey(encKey, source);
            if (verbose)
                Console.WriteLine($"Finalized manifest unlock XorKey: {ByteArrayToString(unlockXorKey)}");
            var recoveredEncKey = ExclusiveOr(source.Select(b =>
            {
                Debug.Assert(b != null, "b != null");
                return b.FullHash.Take(32).ToArray();
            }).Aggregate(ExclusiveOr), unlockXorKey);
            Debug.Assert(encKey.SequenceEqual(recoveredEncKey));

            return new FileManifest
                {
                    Blocks = source,
                    File = fileInfo,
                    UnlockBytes = unlockXorKey
                };
        }

        [NotNull, ItemCanBeNull]
        private static async Task<FileManifest> ChunkFile(
           [NotNull] BufferedStream fileStream,
           [NotNull] FileInfo localShareFileManifest,
           [NotNull] Func<HashAlgorithm, byte[], uint, Task<Block>> blockCreatorTask,
           bool verbose = false,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            if (localShareFileManifest == null)
            {
                throw new ArgumentNullException(nameof(localShareFileManifest));
            }

            if (blockCreatorTask == null)
            {
                throw new ArgumentNullException(nameof(blockCreatorTask));
            }

            LocalShareFileManifest localManifest;
            using (var fs = new FileStream(localShareFileManifest.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs))
            {
                localManifest = JsonConvert.DeserializeObject<LocalShareFileManifest>(sr.ReadToEnd());
                fs.Close();
            }

            Debug.Assert(localManifest != null, "manifest != null");
            if (localManifest.FileManifest == null)
            {
                LocalDirectoryManifest localDirectoryManifest;

                using (var fs = new FileStream(localShareFileManifest.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var sr = new StreamReader(fs))
                {
                    localDirectoryManifest = JsonConvert.DeserializeObject<LocalDirectoryManifest>(sr.ReadToEnd());
                    fs.Close();
                }

                Debug.Assert(localDirectoryManifest != null, "localDirectoryManifest != null");
                Debug.Assert(localDirectoryManifest.LocalFileManifests != null, "localDirectoryManifest.LocalFileManifests != null");
                Debug.Assert(localDirectoryManifest.LocalFileManifests.Count > 0, "localDirectoryManifest.LocalFileManifests.Count > 0");
                Debug.Assert(localDirectoryManifest.LocalFileManifests.Count == 1, "localDirectoryManifest.LocalFileManifests.Count == 1");
                localManifest = localDirectoryManifest.LocalFileManifests[0];
                Debug.Assert(localManifest != null, "manifest != null");
            }

            Debug.Assert(localManifest.FileManifest != null, "localManifest.FileManifest != null");
            Debug.Assert(localManifest.FileManifest.BlockHashes != null, "localManifest.FileManifest.BlockHashes != null");
            Debug.Assert(localManifest.BlockLengths != null, "localManifest.BlockLengths != null");

            var chunkHashes = new ConcurrentDictionary<int, byte[]>(localManifest.FileManifest.BlockHashes.Select((s,i) => new {s, i}).ToDictionary(x => x.i, x => StringToByteArray(x.s)));
            var chunkLengths = new ConcurrentDictionary<int, int>(localManifest.BlockLengths.Select((s, i) => new { s, i }).ToDictionary(x => x.i, x => Convert.ToInt32(x.s)));

            var encKey = ExclusiveOr(chunkHashes.Values.Select(b => b.Take(32).ToArray()).Aggregate(ExclusiveOr), StringToByteArray(localManifest.FileManifest.Unlock));
            if (verbose)
                Console.WriteLine($"Recovered Encryption Key: {ByteArrayToString(encKey)}");

            if (verbose)
            {
                Console.Write("Splitting and encrypting chunks");
                Console.WriteLine($"\r\nEncryption Key: {ByteArrayToString(encKey)}");
            }

            fileStream.Seek(0, SeekOrigin.Begin);
            using (var aes = new AesManaged
            {
                KeySize = 256,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.Zeros
            })
            {
                // Setup IV
                using (var sha = new SHA512Managed())
                {
                    var iv = sha.ComputeHash(encKey).Take(16).ToArray();
                    using (var encryptor = aes.CreateEncryptor(encKey, iv))
                    {
                        if (verbose)
                            Console.WriteLine("Encrypting blocks");

                        var i = 0;
                        foreach (var blockLength in chunkLengths)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return null;

                            if (verbose)
                                Console.WriteLine($"Encrypting block {i + 1} of {chunkLengths.Count} ({(double)i / chunkLengths.Count:P0})");
                            i++;

                            var fileBytes = new byte[blockLength.Value];
                            await fileStream.ReadAsync(fileBytes, 0, blockLength.Value);

                            using (var msBlock = new MemoryStream(blockLength.Value))
                            using (var csBlock = new CryptoStream(msBlock, encryptor, CryptoStreamMode.Write))
                            {
                                csBlock.Write(fileBytes, 0, fileBytes.Length);
                                byte[] encryptedBlockBytes;
                                if (blockLength.Key == chunkLengths.Count - 1)
                                {
                                    csBlock.FlushFinalBlock();
                                    encryptedBlockBytes = new byte[msBlock.Length];
                                }
                                else
                                {
                                    encryptedBlockBytes = new byte[blockLength.Value + Math.Max(0L, msBlock.Length % 128L - blockLength.Value % 128)];
                                }

                                msBlock.ToArray().CopyTo(encryptedBlockBytes, 0);
                                var block = await blockCreatorTask.Invoke(sha, encryptedBlockBytes, Convert.ToUInt32(blockLength.Key));
                                if (block == null)
                                    throw new InvalidOperationException("Block creator returned 'null'");

                                if (verbose)
                                {
                                    Console.WriteLine(block.Path == null ? $" {blockLength.Key} {Path.GetFileName(block.Path)} ({encryptedBlockBytes.Length:N0} bytes)" : $" {blockLength.Key} {Path.GetFileName(block.Path)} ({new FileInfo(block.Path).Length:N0} bytes)");
                                    Console.WriteLine($" \\-{ByteArrayToString(block.FullHash)}");
                                }

                                csBlock.Close();
                            }
                        }
                    }
                }
            }

            return localManifest.FileManifest;
        }

        [NotNull]
        public static async Task<bool> UnchunkFileViaOutputDirectory([NotNull] FileInfo manifestFile, [NotNull] FileInfo saveFile, bool overwrite)
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
            Debug.Assert(manifest != null, "manifest != null");
            Debug.Assert(manifest.BlockHashes != null, "manifest.BlockHashes != null");
            Debug.Assert(manifestFile.Directory != null, "manifestFile.Directory != null");

            return await UnchunkFileViaOutputDirectory(manifest, manifestFile.DirectoryName, saveFile, overwrite);
        }

        public static async Task<bool> UnchunkFileViaOutputDirectory([NotNull] FileManifest manifest, [NotNull] string inputDirectoryPath, [NotNull] FileInfo saveFile, bool overwrite)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (inputDirectoryPath == null)
                throw new ArgumentNullException(nameof(inputDirectoryPath));
            if (!Directory.Exists(inputDirectoryPath))
                throw new DirectoryNotFoundException($"Unable to find the directory {inputDirectoryPath}");
            if (saveFile == null)
                throw new ArgumentNullException(nameof(saveFile));

            var inputDirectory = new DirectoryInfo(inputDirectoryPath);
            var fileInfoList = new List<FileInfo>();
            foreach (var file in Directory.GetFiles(inputDirectory.FullName))
            {
                Debug.Assert(file != null, "file != null");
                if (manifest.BlockHashes.Any(bh =>
                {
                    Debug.Assert(bh != null, "bh != null");
                    return file.IndexOf($"{manifest.Name}.{bh.Substring(0, 8)}", StringComparison.OrdinalIgnoreCase) > -1;
                }))
                    fileInfoList.Add(new FileInfo(file));
            }
            return await UnchunkFile(manifest, fileInfoList, saveFile, overwrite);
        }

        public static void DeleteChunksInOutputDirectory([NotNull] FileManifest manifest, [NotNull] string inputDirectoryPath)
        {
            foreach (var file in Directory.GetFiles(inputDirectoryPath))
            {
                Debug.Assert(file != null, "file != null");
                if (manifest.BlockHashes.Any(bh =>
                {
                    Debug.Assert(bh != null, "bh != null");
                    return file.IndexOf($"{manifest.Name}.{bh.Substring(0, 8)}", StringComparison.OrdinalIgnoreCase) > -1;
                }))
                    File.Delete(file);
            }
        }

        private static async Task<bool> UnchunkFile([NotNull] FileManifest manifest, [NotNull] ICollection<FileInfo> fileBlocks, [NotNull] FileInfo saveFile, bool overwrite = false, bool verbose = false)
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
                {
                    Debug.Assert(fileBlock != null, "fileBlock != null");
                    using (var fs = new FileStream(fileBlock.FullName, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        var hash = sha.ComputeHash(fs);
                        if (verbose)
                            Console.WriteLine($"Hashed block for manifest matching: {fileBlock.Name} ({fileBlock.Length:N0} bytes) -> {ByteArrayToString(hash)}");

                        fs.Seek(-32, SeekOrigin.End);
                        var last32Bytes = new byte[32];
                        await fs.ReadAsync(last32Bytes, 0, 32);

                        dictionary.Add(fileBlock, new Block(fileBlock.FullName, hash, last32Bytes));
                        fs.Close();
                    }
                }
            }

            var i = 0;
            Debug.Assert(manifest.BlockHashes != null, "manifest.BlockHashes != null");
            Debug.Assert(manifest.BlockHashes.Length > 0, "manifest.BlockHashes.Length > 0");
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

            var encKey = ExclusiveOr(source.Select(b => b.FullHash.Take(32).ToArray()).Aggregate(ExclusiveOr), StringToByteArray(manifest.Unlock));
            if (verbose)
                Console.WriteLine($"Recovered Encryption Key: {ByteArrayToString(encKey)}");

            // Setup IV
            byte[] iv;
            using (var sha = new SHA512Managed())
                iv = sha.ComputeHash(encKey).Take(16).ToArray();

            using (var fsSave = new FileStream(saveFile.FullName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
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
                            using (var bsBlock = new BufferedStream(fsBlock))
                            using (var csBlock = new CryptoStream(bsBlock, decryptor, CryptoStreamMode.Read))
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
                                        decryptReadActual = await csBlock.ReadAsync(buffer, 0, decryptReadMax);
                                        totalDecryptedRead += decryptReadActual;
                                        if (verbose)
                                        {
                                            Console.Write(".");
                                        }

                                        await fsSave.WriteAsync(buffer, 0, decryptReadActual);
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
        public static byte[] ExclusiveOr([NotNull] byte[] arr1, [NotNull] byte[] arr2)
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
