namespace Mystiko.IO
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    public static class DirectoryUtility
    {
        public static async Task<LocalDirectoryManifest> PreHashDirectory(
            [NotNull] string inputDirectoryPath, 
            [CanBeNull] Action<string> enteringDirectoryAction = null, 
            [CanBeNull] Action<string> hashingFileAction = null, 
            int? chunkSize = null)
        {
            if (string.IsNullOrEmpty(inputDirectoryPath))
                throw new ArgumentNullException(nameof(inputDirectoryPath));
            if (!Directory.Exists(inputDirectoryPath))
                throw new DirectoryNotFoundException($"Unable to locate directory {inputDirectoryPath}");

            var fileManifests = new List<LocalShareFileManifest>();

            foreach (var filePath in Directory.GetFiles(inputDirectoryPath))
            {
                if (filePath == null)
                    continue;

                hashingFileAction?.Invoke(filePath);
                fileManifests.Add(await PreHashFile(filePath, chunkSize));
            }

            foreach (var directoryPath in Directory.GetDirectories(inputDirectoryPath))
            {
                if (directoryPath == null)
                    continue;

                enteringDirectoryAction?.Invoke(directoryPath);
                fileManifests.AddRange((await PreHashDirectory(directoryPath, enteringDirectoryAction, hashingFileAction, chunkSize)).LocalFileManifests);
            }
            
            return new LocalDirectoryManifest
                       {
                           LocalFileManifests = fileManifests
                       };
        }

        public static async Task<LocalShareFileManifest> PreHashFile([NotNull] string inputFilePath, int? chunkSize, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(inputFilePath))
                throw new ArgumentNullException(nameof(inputFilePath));
            if (!File.Exists(inputFilePath))
                throw new DirectoryNotFoundException($"Unable to locate file {inputFilePath}");

            var inputFileInfo = new FileInfo(inputFilePath);

            var chunkLengths = new List<int>();
            foreach (var chunkLength in FileUtility.GetChunkLengths(inputFileInfo.Length, chunkSize))
            {
                chunkLengths.Add(chunkLength);
            }

            var manifest = await FileUtility.ChunkFileMetadataOnly(inputFileInfo, cancellationToken);

            // Hash file to detect future changes to the content of it
            string hashString;
            using (var sha = SHA512.Create())
            using (var fs = inputFileInfo.OpenRead())
            {
                var hash = sha.ComputeHash(fs);
                hashString = FileUtility.ByteArrayToString(hash);
            }

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
