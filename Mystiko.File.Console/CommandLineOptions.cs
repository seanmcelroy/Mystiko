namespace Mystiko.File.Console
{
    using System.Diagnostics;

    using CommandLine;
    using CommandLine.Text;

    class CommandLineOptions
    {
        /// <summary>
        /// Gets or sets the path to a manifest file to decrypt a package
        /// </summary>
        [Option('d', "decrypt", HelpText = "The path to a Mystiko manifest file to decrypt a package")]
        public string DecryptFile { get; set; }

        /// <summary>
        /// Gets or sets the path to file to encrypt and package
        /// </summary>
        [Option('e', "encrypt", HelpText = "The path to file to encrypt and package")]
        public string EncryptFile { get; set; }

        /// <summary>
        /// Gets or sets the path to pre-hash
        /// </summary>
        [Option('h', "hash", HelpText = "The path to hash for a manifest output, without actually creating split encrypted files")]
        public string PrehashPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to overwrite files if required
        /// </summary>
        [Option('f', "force", HelpText = "Overwrite files if required")]
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets the path for the unpackaged file, if used with the --decrypt operation
        /// </summary>
        [Option('o', "output", HelpText = "If used with the --decrypt operation, specifies the path for the unpackaged file")]
        public string OutputFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to pause at the end of the operation
        /// </summary>
        [Option('p', "pause", HelpText = "Pauses at the end of the operation")]
        public bool Pause { get; set; }

        /// <summary>
        /// Gets or sets an option indicating the size of the split block files, in bytes.  If not specified, each block file will be a random size between 1 MB and 10 MB
        /// </summary>
        [Option('s', "size", HelpText = "Size of the split block files, in bytes.  If not specified, each block file will be a random size between 1 MB and 10 MB")]
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
