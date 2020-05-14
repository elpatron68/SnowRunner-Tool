using CommandLine;

namespace SnowRunner_Tool
{
    public class Options
    {
        [Option('i', "import", Required = false, HelpText = "3rd party backup directory to import.")]
        public string ThirdPartyDirectory { get; set; }

        [Option('l', "log", Required = false, HelpText = "Enable remote logging")]
        public bool EnableLogging { get; set; }

    }
}
