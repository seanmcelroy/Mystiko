namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    public class Block(string? path, byte[] hash, byte[] last64Bytes)
    {
        public string? Path { get; } = path;

        public byte[] FullHash { get; internal set; } = hash ?? throw new ArgumentNullException(nameof(hash));

        /// <summary>
        /// Gets the last 64 bytes of the encrypted block, used to obscure the unlock key
        /// </summary>
        public byte[] Last64Bytes { get; private set; } = last64Bytes ?? throw new ArgumentNullException(nameof(last64Bytes));

        /// <summary>
        /// The sequence ordering of the block
        /// </summary>
        public int Ordering { get; set; }

        public static async Task<Block> NoSavedChunk(
           HashAlgorithm hasher,
           byte[] encryptedChunk,
           string? chunkFileName,
           uint ordering)
        {
            ArgumentNullException.ThrowIfNull(encryptedChunk);
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var last64Bytes = new byte[64];
            if (encryptedChunk.Length < 64)
            {
                Array.Copy(encryptedChunk, 0, last64Bytes, 64 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 64, last64Bytes, 0, 64);
            }

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");
            var block = new Block(null, encryptedChunkHash, last64Bytes);

            return block;
        }

        public static async Task<Block> CreateViaTemp(
            HashAlgorithm hasher,
            byte[] encryptedChunk,
            string? chunkFileName,
            uint ordering)
        {
            ArgumentNullException.ThrowIfNull(encryptedChunk, nameof(encryptedChunk));
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var last64Bytes = new byte[64];
            if (encryptedChunk.Length < 64)
            {
                Array.Copy(encryptedChunk, 0, last64Bytes, 64 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 64, last64Bytes, 0, 64);
            }

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");
            var block = new Block(System.IO.Path.GetTempFileName(), encryptedChunkHash, last64Bytes);

            using (var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                await fs.WriteAsync(encryptedChunk);
            }

            return block;
        }

        public static async Task<Block> CreateViaOutputDirectory(
            HashAlgorithm hasher,
            byte[] encryptedChunk,
            DirectoryInfo outputDirectory,
            string chunkFileName,
            bool overwrite,
            bool verbose = false,
            bool verify = false)
        {
            ArgumentNullException.ThrowIfNull(hasher);
            ArgumentNullException.ThrowIfNull(encryptedChunk);
            if (encryptedChunk.Length == 0)
                throw new ArgumentException("Zero byte file", nameof(encryptedChunk));

            var encryptedChunkHash = hasher.ComputeHash(encryptedChunk, 0, encryptedChunk.Length);
            Debug.Assert(encryptedChunkHash != null, "encryptedChunkHash != null");

            var chunkPath = System.IO.Path.Combine(outputDirectory.FullName, chunkFileName);
            if (File.Exists(chunkPath))
            {
                if (!overwrite)
                {
                    throw new ArgumentException($"Block file already exists at {chunkPath}", nameof(chunkFileName));
                }

                if (verbose)
                {
                    Console.WriteLine($"Deleting block file that already exists at {chunkPath}");
                }

                File.Delete(chunkPath);
            }

            var last64Bytes = new byte[64];
            if (encryptedChunk.Length < 64)
            {
                Array.Copy(encryptedChunk, 0, last64Bytes, 64 - encryptedChunk.Length, encryptedChunk.Length);
            }
            else
            {
                Array.Copy(encryptedChunk, encryptedChunk.Length - 64, last64Bytes, 0, 64);
            }

            var block = new Block(chunkPath, encryptedChunkHash, last64Bytes);
            Debug.Assert(block.Path != null, "block.Path != null");

            using (var fs = new FileStream(block.Path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var bs = new BufferedStream(fs))
            {
                await bs.WriteAsync(encryptedChunk);
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
                    Debug.Assert(blockFileHash != null, "blockFileHash != null");
                    if (verbose)
                    {
                        Console.WriteLine($"Hash for: {new FileInfo(block.Path).Name}: {FileUtility.ByteArrayToString(blockFileHash)[..8]}");
                    }
                }

                if (!encryptedChunkHash.SequenceEqual(blockFileHash))
                {
                    Console.WriteLine($"WARNING: Hash does not match for {new FileInfo(block.Path).Name}: {FileUtility.ByteArrayToString(encryptedChunkHash)[..8]} vs {FileUtility.ByteArrayToString(blockFileHash)[..8]}");
                }
            }

            return block;
        }

        public static byte[] CalculateUnlockXorKey(byte[] encKey, IEnumerable<Block> allBlocks)
        {
            ArgumentNullException.ThrowIfNull(encKey);
            ArgumentNullException.ThrowIfNull(allBlocks);

            var result = allBlocks.Aggregate(
                encKey,
                (current, block) =>
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