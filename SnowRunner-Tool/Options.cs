using CommandLine.Text;
using CommandLine;

namespace SnowRunner_Tool
{
    public class Options
    {
        [Option('s', "steam", Required = false, HelpText = "Snowrunner from Steam")]
        public bool Steam { get; set; }
    }
}
