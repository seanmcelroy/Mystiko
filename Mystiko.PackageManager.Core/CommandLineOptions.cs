// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CommandLineOptions.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Command line options for the console application
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using CommandLine.Text;

[assembly: AssemblyLicense("Released as open-source software under the licensing terms of the MIT License.")]

namespace Mystiko.PackageManager
{
    using System.Collections.Generic;

    using CommandLine;
    using CommandLine.Text;

    /// <summary>
    /// Command line options for the console application
    /// </summary>
    internal class CommandLineOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Decrypts a split file set
        /// </summary>
        [Option('d', "decrypt", HelpText = "Decrypts a Mystiko split file set", SetName = "operation")]
        public bool Decrypt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Encrypts a file into split files and a manifest
        /// </summary>
        [Option('e', "encrypt", HelpText = "Encrypts a file into split files and a manifest", SetName = "operation")]
        public bool Encrypt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Prepares a manifest, but does not create split files
        /// </summary>
        [Option('h', "hash", HelpText = "Prepares a manifest, but does not create split files", SetName = "operation")]
        public bool Hash { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Creates split files from a prepared manifest
        /// </summary>
        [Option('c', "createFromHash", HelpText = "Creates split files from a prepared manifest", SetName = "operation")]
        public bool CreateFromHash { get; set; }

        /// <summary>
        /// Gets or sets the source file or directory to use in an Encrypt or Hash operation
        /// </summary>
        [Option('s', "source", HelpText = "The source file or directory to use in an Encrypt or Hash operation")]
        public string SourcePath { get; set; }

        /// <summary>
        /// Gets or sets the manifest file to use in a Decrypt or Hash operation
        /// </summary>
        [Option('m', "manifest", HelpText = "The manifest file to use in a Decrypt or Hash operation")]
        public string ManifestFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to overwrite files if required
        /// </summary>
        [Option('f', "force", HelpText = "Overwrite files if required")]
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets the path for the unpackaged file, if used with the --decrypt operation
        /// </summary>
        [Option('o', "output", HelpText = "The output file to use in a Decrypt operation, specifying the path for the unpackaged file")]
        public string OutputFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to pause at the end of the operation
        /// </summary>
        [Option('p', "pause", HelpText = "Pauses at the end of the operation")]
        public bool Pause { get; set; }

        /// <summary>
        /// Gets or sets an option indicating the size of the split block files, in bytes.  If not specified, each block file will be a random size between 1 MB and 10 MB
        /// </summary>
        [Option('z', "size", HelpText = "Size of the split block files, in bytes.  If not specified, each block file will be a random size between 1 MB and 10 MB")]
        public int? Size { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to write verbose output
        /// </summary>
        [Option('v', "verbose", HelpText = "Write verbose output")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to perform extra verification checks on internal operations
        /// </summary>
        [Option('y', "verify", HelpText = "Perform extra verification checks on internal operations")]
        public bool Verify { get; set; }

        [Usage(ApplicationAlias = "mpm.exe")]
        public static IEnumerable<Example> Usages
        {
            get
            {
                yield return
                    new Example(
                        "To encrypt a file into split parts and a manifest",
                        new CommandLineOptions { Encrypt = true, SourcePath = @"C:\Downloads\secret.file" });
                yield return
                    new Example(
                        "To decrypt a file from its split parts and manifest",
                        new CommandLineOptions { Decrypt = true, ManifestFile = @"C:\Downloads\secret.file.mystiko" });
                yield return
                    new Example(
                        "To decrypt a file from its split parts and manifest to a specific path",
                        new CommandLineOptions
                            {
                                Decrypt = true,
                                ManifestFile = @"C:\Downloads\secret.file.mystiko",
                                OutputFile = @"C:\Downloads\secret.file.rebuilt"
                            });
                yield return
                    new Example(
                        "To create a special 'local' manifest file of a file without actually creating the encrypted split files",
                        new CommandLineOptions
                            {
                                Hash = true,
                                SourcePath = @"C:\Downloads\secret.file",
                                ManifestFile = @"C:\Downloads\secret.file.mystiko2"
                            });
                yield return
                    new Example(
                        "To create a split parts from a pre-calculated 'local' manifest",
                        new CommandLineOptions
                            {
                                CreateFromHash = true,
                                SourcePath = @"C:\Downloads\secret.file",
                                ManifestFile = @"C:\Downloads\secret.file.mystiko2"
                            });
            }
        }
    }
}
