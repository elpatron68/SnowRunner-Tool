using CommandLine.Text;
using CommandLine;

namespace SnowRunner_Tool
{
    public class Options
    {
        [Option("platform", Required = true, HelpText = "Set your platform (Epic/Steam/Microsoft)")]
        public string Platform { get; set; }
    }
}
