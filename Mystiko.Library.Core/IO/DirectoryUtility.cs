namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mystiko.Cryptography;

    public static class DirectoryUtility
    {
        [Pure, NotNull, ItemNotNull]
        public static async Task<LocalDirectoryManifest> PreHashDirectory(
            [NotNull] string inputPath, 
            [CanBeNull] Action<string> enteringDirectoryAction = null, 
            [CanBeNull] Action<string> hashingFileAction = null, 
            bool verbose = false,
            int? chunkSize = null)
        {
            if (string.IsNullOrEmpty(inputPath))
                throw new ArgumentNullException(nameof(inputPath));

            var fileManifests = new List<LocalShareFileManifest>();

            if (File.Exists(inputPath))
            {
                hashingFileAction?.Invoke(inputPath);
                fileManifests.Add(await PreHashFile(inputPath, chunkSize, verbose));
            }
            else
            {
                if (!Directory.Exists(inputPath))
                {
                    throw new DirectoryNotFoundException($"Unable to locate directory {inputPath}");
                }

                foreach (var filePath in Directory.GetFiles(inputPath))
                {
                    if (filePath == null)
                        continue;

                    hashingFileAction?.Invoke(filePath);
                    fileManifests.Add(await PreHashFile(filePath, chunkSize, verbose));
                }

                foreach (var directoryPath in Directory.GetDirectories(inputPath))
                {
                    if (directoryPath == null)
                        continue;

                    enteringDirectoryAction?.Invoke(directoryPath);
                    fileManifests.AddRange((await PreHashDirectory(directoryPath, enteringDirectoryAction, hashingFileAction, verbose, chunkSize)).LocalFileManifests);
                }
            }

            return new LocalDirectoryManifest
                       {
                           LocalFileManifests = fileManifests
                       };
        }

        public static async Task<LocalShareFileManifest> PreHashFile([NotNull] string inputFilePath, int? chunkSize, bool verbose = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(nameof(inputFilePath));
            if (!File.Exists(inputFilePath))
                throw new DirectoryNotFoundException($"Unable to locate file {inputFilePath}");

            var inputFileInfo = new FileInfo(inputFilePath);

            var chunkLengths = new List<int>();
            foreach (var chunkLength in FileUtility.GetChunkLengths(inputFileInfo.Length, chunkSize))
            {
                try
                {
                    chunkLengths.Add(chunkLength);
                }
                catch (OutOfMemoryException oom)
                {
                    throw new OutOfMemoryException($"Unable to add another chunk; list is {chunkLengths.Count:N0} items long", oom);
                }
            }

            var manifest = await FileUtility.ChunkFileMetadataOnly(inputFileInfo, verbose, cancellationToken);

            // Hash file to detect future changes to the content of it, using 16 MB buffer
            var hash = (await HashUtility.HashFileSHA512Async(inputFileInfo)).Item1;
            Debug.Assert(hash != null, "hash != null");
            var hashString = FileUtility.ByteArrayToString(hash);

            return new LocalShareFileManifest
            {
                FileManifest = manifest,
                BlockLengths = chunkLengths.Select(i => i.ToString()).ToArray(),
                LocalPath = inputFilePath,
                SizeBytes = inputFileInfo.Length,
                Hash = hashString
            };
        }
    }
}
