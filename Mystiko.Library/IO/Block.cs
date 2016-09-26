
namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;

    using JetBrains.Annotations;

    public class Block
    {
        [CanBeNull]
        public string Path { get; private set; }

        [NotNull]
        public byte[] FullHash { get; private set; }

        /// <summary>
        /// The last 32 bytes of the encrypted block, used to obscure the unlock key
        /// </summary>
        [NotNull]
        public byte[] Last32Bytes { get; private set; }

        /// <summary>
        /// The sequence ordering of the block
        /// </summary>
        public int Ordering { get; set; }

        public Block([CanBeNull] string path, [NotNull] byte[] hash, [NotNull] byte[] last32bytes)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));
            if (last32bytes == null)
                throw new ArgumentNullException(nameof(last32bytes));

            this.Path = path;
            this.FullHash = hash;
            this.Last32Bytes = last32bytes;
        }

        [NotNull]
        public static Block CreateViaTemp(
            [NotNull] HashAlgorithm hasher,
            [NotNull] byte[] encryptedChunk,
            uint ordering)
        {
            if (encryptedChunk == null)
                throw new ArgumentNullException(nameof(encryptedChunk));
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var last32Bytes = new byte[32];
            Array.Copy(encryptedChunk, encryptedChunk.Length - 32, last32Bytes, 0, 32);

            var block = new Block(System.IO.Path.GetTempFileName(), hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length), last32Bytes);

            using (var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(encryptedChunk, 0, encryptedChunk.Length);
                bw.Write(ordering);
                fs.Close();
            }

            return block;
        }

        [NotNull]
        public static Block CreateViaOutputDirectory(
            [NotNull] HashAlgorithm hasher,
            [NotNull] byte[] encryptedChunk,
            [NotNull] string path,
            [NotNull] string baseFileName,
            bool overwrite,
            bool verbose = false,
            bool verify = false)
        {
            if (hasher == null)
                throw new ArgumentNullException(nameof(hasher));
            if (encryptedChunk == null)
                throw new ArgumentNullException(nameof(encryptedChunk));
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            var blockPath = System.IO.Path.Combine(path, $"{baseFileName}.{FileUtility.ByteArrayToString(encryptedChunkHash).Substring(0, 8)}");
            if (File.Exists(blockPath))
            {
                if (!overwrite)
                    throw new ArgumentException("Block file already exists at " + blockPath, nameof(baseFileName));
                File.Delete(blockPath);
            }

            var last32Bytes = new byte[32];
            Array.Copy(encryptedChunk, encryptedChunk.Length - 32, last32Bytes, 0, 32);

            var block = new Block(blockPath, encryptedChunkHash, last32Bytes);
            using (var fs = new FileStream(block.Path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(encryptedChunk, 0, encryptedChunk.Length);
                fs.Close();
            }

            // Verify the file output hash
            if (verify)
            {
                if (encryptedChunk.Length != new FileInfo(block.Path).Length)
                    Console.WriteLine($"WARNING: Data length does not match for data (LEN={encryptedChunk.Length}) vs file {new FileInfo(block.Path).Name} (LEN={new FileInfo(block.Path).Length})");

                byte[] blockFileHash;
                using (var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Read, FileShare.Write))
                {
                    blockFileHash = hasher.ComputeHash(fs);
                    if (verbose)
                        Console.WriteLine($"Hash for: {new FileInfo(block.Path).Name}: {FileUtility.ByteArrayToString(blockFileHash).Substring(0, 8)}");
                    fs.Close();
                }

                if (!encryptedChunkHash.SequenceEqual(blockFileHash))
                    Console.WriteLine($"WARNING: Hash does not match for {new FileInfo(block.Path).Name}: {FileUtility.ByteArrayToString(encryptedChunkHash).Substring(0, 8)} vs {FileUtility.ByteArrayToString(blockFileHash).Substring(0, 8)}");
            }

            return block;
        }

        [NotNull, Pure]
        public static byte[] CalculateUnlockXorKey([NotNull] byte[] encKey, [NotNull] IEnumerable<Block> allBlocks)
        {
            if (encKey == null)
                throw new ArgumentNullException(nameof(encKey));
            if (allBlocks == null)
                throw new ArgumentNullException(nameof(allBlocks));

            return allBlocks.Aggregate(encKey, (current, block) => FileUtility.ExclusiveOR(current, block.FullHash.Take(encKey.Length).ToArray()));
        }
    }
}