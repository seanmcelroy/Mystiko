// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CommandLineOptions.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Command line options for the console application
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.PackageManager
{
    using System.Diagnostics;

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
        [Option('d', "decrypt", HelpText = "Decrypts a Mystiko split file set", MutuallyExclusiveSet = "operation")]
        public bool Decrypt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Encrypts a file into split files and a manifest
        /// </summary>
        [Option('e', "encrypt", HelpText = "Encrypts a file into split files and a manifest", MutuallyExclusiveSet = "operation")]
        public bool Encrypt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Prepares a manifest, but does not create split files
        /// </summary>
        [Option('h', "hash", HelpText = "Prepares a manifest, but does not create split files", MutuallyExclusiveSet = "operation")]
        public bool Hash { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to execute the operation: Creates split files from a prepared manifest
        /// </summary>
        [Option('c', "createFromHash", HelpText = "Creates split files from a prepared manifest", MutuallyExclusiveSet = "operation")]
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

        [HelpOption]
        public string GetUsage()
        {
            var helpText = HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
            Debug.Assert(helpText != null, "helpText != null");
            helpText.Copyright = new CopyrightInfo("Sean McElroy", 2016);
            return helpText;
        }
    }
}
