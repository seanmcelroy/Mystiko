namespace Mystiko.File.Console
{
    using CommandLine;
    using CommandLine.Text;

    class CommandLineOptions
    {
        [Option('d', "decrypt", HelpText = "The path to a Mystiko manifest file to decrypt a package")]
        public string DecryptFile { get; set; }

        [Option('e', "encrypt", HelpText = "The path to file to encrypt and package")]
        public string EncryptFile { get; set; }

        [Option('f', "force", HelpText = "Overwrite files if required")]
        public bool Force { get; set; }

        [Option('o', "output", HelpText = "If used with the --decrypt operation, specifies the path for the unpackaged file")]
        public string OutputFile { get; set; }

        [Option('p', "pause", HelpText = "Pauses at the end of the operation")]
        public bool Pause { get; set; }

        [Option('v', "verbose", HelpText = "Write verbose output")]
        public bool Verbose { get; set; }

        [Option('y', "verify", HelpText = "Perform extra verification checks on internal operations")]
        public bool Verify { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var helpText = HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
            helpText.Copyright = new CopyrightInfo("Sean McElroy", 2016);
            return helpText;
        }
    }
}
