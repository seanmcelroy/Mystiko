
namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    public class Block
    {
        [CanBeNull]
        public string Path { get; }

        [NotNull]
        public byte[] FullHash { get; }

        /// <summary>
        /// The last 32 bytes of the encrypted block, used to obscure the unlock key
        /// </summary>
        [NotNull]
        private byte[] Last32Bytes { get; set; }

        /// <summary>
        /// The sequence ordering of the block
        /// </summary>
        public int Ordering { get; set; }

        public Block([CanBeNull] string path, [NotNull] byte[] hash, [NotNull] byte[] last32Bytes)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));
            if (last32Bytes == null)
                throw new ArgumentNullException(nameof(last32Bytes));

            this.Path = path;
            this.FullHash = hash;
            this.Last32Bytes = last32Bytes;
        }

        [NotNull]
        public static async Task<Block> NoSavedChunk(
           [NotNull] HashAlgorithm hasher,
           [NotNull] byte[] encryptedChunk,
            uint ordering)
        {
            if (encryptedChunk == null)
                throw new ArgumentNullException(nameof(encryptedChunk));
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var last32Bytes = new byte[32];
            if (encryptedChunk.Length < 32)
            {
                Array.Copy(encryptedChunk, 0, last32Bytes, 32 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 32, last32Bytes, 0, 32);
            }

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");
            var block = new Block(null, encryptedChunkHash, last32Bytes);

            return block;
        }

        [NotNull]
        public static async Task<Block> CreateViaTemp(
            [NotNull] HashAlgorithm hasher,
            [NotNull] byte[] encryptedChunk,
            uint ordering)
        {
            if (encryptedChunk == null)
                throw new ArgumentNullException(nameof(encryptedChunk));
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var last32Bytes = new byte[32];
            if (encryptedChunk.Length < 32)
            {
                Array.Copy(encryptedChunk, 0, last32Bytes, 32 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 32, last32Bytes, 0, 32);
            }

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");
            var block = new Block(System.IO.Path.GetTempFileName(), encryptedChunkHash, last32Bytes);

            using (var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(encryptedChunk, 0, encryptedChunk.Length);
                fs.Close();
            }

            return block;
        }

        [NotNull]
        public static async Task<Block> CreateViaOutputDirectory(
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
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");

            var blockPath = System.IO.Path.Combine(path, $"{baseFileName}.{FileUtility.ByteArrayToString(encryptedChunkHash).Substring(0, 8)}");
            if (File.Exists(blockPath))
            {
                if (!overwrite)
                    throw new ArgumentException("Block file already exists at " + blockPath, nameof(baseFileName));
                File.Delete(blockPath);
            }

            var last32Bytes = new byte[32];
            if (encryptedChunk.Length < 32)
            {
                Array.Copy(encryptedChunk, 0, last32Bytes, 32 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 32, last32Bytes, 0, 32);
            }

            var block = new Block(blockPath, encryptedChunkHash, last32Bytes);
            Debug.Assert(block.Path != null, "block.Path != null");

            using (var fs = new FileStream(block.Path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var bs = new BufferedStream(fs))
            {
                await bs.WriteAsync(encryptedChunk, 0, encryptedChunk.Length);
                fs.Close();
            }

            // Verify the file output hash
            if (verify)
            {
                if (encryptedChunk.Length != new FileInfo(block.Path).Length)
                    Console.WriteLine($"WARNING: Data length does not match for data (LEN={encryptedChunk.Length}) vs file {new FileInfo(block.Path).Name} (LEN={new FileInfo(block.Path).Length})");

                byte[] blockFileHash;
                using (var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Read, FileShare.Write))
                using (var bs = new BufferedStream(fs))
                {
                    blockFileHash = hasher.ComputeHash(bs);
                    if (verbose)
                        Console.WriteLine($"Hash for: {new FileInfo(block.Path).Name}: {FileUtility.ByteArrayToString(blockFileHash).Substring(0, 8)}");
                    bs.Close();
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

            var result = allBlocks.Aggregate(encKey, (current, block) =>
            {
                Debug.Assert(current != null, "current != null");
                Debug.Assert(block != null, "block != null");
                Debug.Assert(block.FullHash != null, "block.FullHash != null");
                return FileUtility.ExclusiveOr(current, block.FullHash.Take(encKey.Length).ToArray());
            });

            Debug.Assert(result != null, "result != null");
            return result;
        }
    }
}