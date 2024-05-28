// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A basic command line utility for executing methods of the Mystiko library
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.PackageManager
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using CommandLine;

    using IO;

    using Newtonsoft.Json;

    using File = System.IO.File;

    /// <summary>
    /// A basic command line utility for executing methods of the Mystiko library
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Console program entry point
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithNotParsed(
                    errors =>
                        {
                            foreach (var error in errors
                                .Where(e => e.Tag != ErrorType.HelpRequestedError)
                                .Where(e => e.Tag != ErrorType.HelpVerbRequestedError))
                            {
                                Console.WriteLine(error);
                            }

                            Console.WriteLine("Use --help for more information\r\n");
                            Console.ReadLine();
                            Environment.Exit(1);
                        })
                        .WithParsed(
                    options =>
                        {
                            if (options.Encrypt)
                            {
                                Encrypt(options);
                                if (options.Pause)
                                {
                                    Console.ReadLine();
                                }
                            }
                            else if (options.Decrypt)
                            {
                                Decrypt(options);
                                if (options.Pause)
                                {
                                    Console.ReadLine();
                                }
                            }
                            else if (options.Hash)
                            {
                                Prehash(options);
                                if (options.Pause)
                                {
                                    Console.ReadLine();
                                }
                            }
                            else if (options.CreateFromHash)
                            {
                                ChunkFromPrehash(options);
                                if (options.Pause)
                                {
                                    Console.ReadLine();
                                }
                            }
                            else
                            {
                                // No operation specified
                                Console.WriteLine("No operation specified.  Use the --encrypt or --decrypt operation parameter.  Or, use --help for more information\r\n");
                                if (options.Pause)
                                {
                                    Console.ReadLine();
                                }
                            }
                        });
        }

        /// <summary>
        /// Encrypts a file into a manifest and encrypted split files
        /// </summary>
        /// <param name="options">The command line options to configure the encryption process</param>
        private static void Encrypt(CommandLineOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!File.Exists(options.SourcePath))
            {
                Console.WriteLine("Unable to locate file: {0}", options.SourcePath);
                return;
            }

            var encryptFile = new FileInfo(options.SourcePath);
            if (encryptFile.Directory == null || string.IsNullOrWhiteSpace(encryptFile.DirectoryName))
            {
                Console.WriteLine("Unable to locate parent directory of file: {0}", options.SourcePath);
                return;
            }

            var manifestFile = new FileInfo(Path.Combine(encryptFile.Directory.FullName, encryptFile.Name + ".mystiko"));
            if (manifestFile.Exists)
            {
                if (!options.Force)
                {
                    Console.WriteLine("Manifest file already exists: {0}", manifestFile.FullName);
                    return;
                }

                manifestFile.Delete();
            }

            Console.WriteLine("Encrypting {0}... ", options.SourcePath);
            Task.Run(async () =>
            {
                var chunkResult = await FileUtility.ChunkFileViaOutputDirectory(encryptFile, encryptFile.Directory, options.Force, options.Verbose, options.Verify, options.Size);

                using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
                using (var sw = new StreamWriter(fs))
                {
                    var json = JsonConvert.SerializeObject(chunkResult);
                    if (json != null)
                    {
                        await sw.WriteAsync(json);
                    }
                }
            }).Wait();

            Console.WriteLine("Encryption complete.");
        }

        /// <summary>
        /// Decrypts a file from a manifest and split files
        /// </summary>
        /// <param name="options">Command line options used for decrypting a file from its manifest and split files</param>
        private static void Decrypt(CommandLineOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!File.Exists(options.ManifestFile))
            {
                Console.WriteLine("Unable to locate file: {0}", options.ManifestFile);
                return;
            }

            var output = string.IsNullOrWhiteSpace(options.OutputFile)
                ? (File.Exists(options.ManifestFile.Replace(".mystiko", string.Empty))
                    ? options.ManifestFile.Replace(".mystiko", string.Empty) + ".decrypted"
                    : options.ManifestFile.Replace(".mystiko", string.Empty))
                : options.OutputFile;

            if (File.Exists(output))
            {
                if (!options.Force)
                {
                    Console.WriteLine("Output file already exists: {0}", output);
                    return;
                }

                File.Delete(output);
                Console.WriteLine("Deleted output file that already existed: {0}", output);
            }

            var unchunkResult = false;
            Task.Run(
                async () =>
                {
                    unchunkResult = await FileUtility.UnchunkFileViaOutputDirectory(new FileInfo(options.ManifestFile), new FileInfo(output), options.Force, options.Verbose);
                }).Wait();

            if (unchunkResult)
            {
                Console.WriteLine("Decryption complete: {0}", output);
            }
            else
            {
                Console.WriteLine("Decryption failed");
            }
        }

        /// <summary>
        /// Pre-hashes a directory without actually creating encrypted split files
        /// </summary>
        /// <param name="options">Command line options used for identifying and pre-hashing the directory</param>
        private static void Prehash(CommandLineOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.SourcePath))
            {
                Console.WriteLine("No source path was specified for the pre-hash operation");
                return;
            }

            Console.WriteLine("Pre-hash {0}... ", options.SourcePath);

            if (File.Exists(options.SourcePath))
            {
                Task.Run(async () =>
                {
                    var localManifest = await DirectoryUtility.PreHashDirectory(
                        options.SourcePath,
                        s => { },
                        s => Console.WriteLine($"\tFile: {s}"),
                        options.Verbose);
                    var manifestFile = new FileInfo(options.ManifestFile ?? options.SourcePath + ".mystiko");

                    if (manifestFile.Exists)
                    {
                        if (!options.Force)
                        {
                            Console.WriteLine("File manifest already exists: {0}", manifestFile.FullName);
                            Environment.Exit(3);
                        }

                        manifestFile.Delete();
                    }

                    using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
                    using (var sw = new StreamWriter(fs))
                    {
                        await sw.WriteAsync(JsonConvert.SerializeObject(localManifest));
                    }
                }).Wait();

                Console.WriteLine("File hashing complete.");
                return;
            }

            if (Directory.Exists(options.SourcePath))
            {
                Task.Run(async () =>
                {
                    var localManifest = await DirectoryUtility.PreHashDirectory(
                        options.SourcePath,
                        s => Console.WriteLine($"Directory: {s}"),
                        s => Console.WriteLine($"\tFile: {s}"),
                        options.Verbose);
                    var manifestFile = new FileInfo(Path.Combine(options.SourcePath, "directory.mystiko"));

                    if (manifestFile.Exists)
                    {
                        if (!options.Force)
                        {
                            Console.WriteLine("Directory manifest file already exists: {0}", manifestFile.FullName);
                            Environment.Exit(2);
                        }

                        manifestFile.Delete();
                    }

                    using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
                    using (var sw = new StreamWriter(fs))
                    {
                        await sw.WriteAsync(JsonConvert.SerializeObject(localManifest));
                    }
                }).Wait();

                Console.WriteLine("Directory hashing complete.");

                return;
            }

            Console.WriteLine("Unable to locate directory: {0}", options.SourcePath);
        }

        private static void ChunkFromPrehash(CommandLineOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.SourcePath))
            {
                Console.WriteLine("No source path was specified for the chunking operation");
                return;
            }

            if (!File.Exists(options.SourcePath))
            {
                Console.WriteLine("Unable to locate file: {0}", options.SourcePath);
                return;
            }

            var sourceFile = new FileInfo(options.SourcePath);
            if (sourceFile.Directory == null || string.IsNullOrWhiteSpace(sourceFile.DirectoryName))
            {
                Console.WriteLine("Unable to locate parent directory of file: {0}", options.SourcePath);
                return;
            }

            if (string.IsNullOrWhiteSpace(options.ManifestFile))
            {
                Console.WriteLine("No manifest file was specified for the chunking operation");
                return;
            }

            var manifestFile = new FileInfo(options.ManifestFile);
            if (manifestFile.Directory == null || string.IsNullOrWhiteSpace(manifestFile.DirectoryName))
            {
                Console.WriteLine("Unable to locate parent directory of file: {0}", options.ManifestFile);
                return;
            }

            if (!manifestFile.Exists)
            {
                Console.WriteLine("Manifest file not found: {0}", manifestFile.FullName);
                return;
            }

            Console.WriteLine("Chunking {0}... ", options.ManifestFile);

            Task.Run(async () =>
            {
                var chunkResult = await FileUtility.ChunkFileViaOutputDirectoryFromPreHash(sourceFile, manifestFile, sourceFile.Directory, options.Force, options.Verbose, options.Verify);

                using (var fs = new FileStream(manifestFile.FullName, FileMode.CreateNew))
                using (var sw = new StreamWriter(fs))
                {
                    var json = JsonConvert.SerializeObject(chunkResult);
                    if (json != null)
                    {
                        await sw.WriteAsync(json);
                    }
                }
            }).Wait();
        }
    }
}