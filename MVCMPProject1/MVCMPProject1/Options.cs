using CommandLine;
using CommandLine.Text;

namespace MVCMPProject1
{
    class Options
    {
        [Option('u', "url", Required = true,
             HelpText = "Url link to download")]
        public string Url { get; set; }

        [Option('o', "output", Required = true,
             HelpText = "Output Folder")]
        public string OutputFolder { get; set; }

        [Option('r', "recursive", Required = false,
             HelpText = "Recursive download")]
        public bool Recursive { get; set; }

        [Option('l', "depth", Required = false, DefaultValue = 2,
             HelpText="Depth of recursive download, default value 2")]
        public int Depth { get; set; }

        [Option('v', "verbose", Required = false,
             HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [Option('k', "convertLink", Required = false,
             HelpText="Convert links for local viewing ")]
        public bool Convertlink { get; set; }

        [Option('p', "page", Required = false,
             HelpText="Download external images, css, and js")]
        public bool Page { get; set; }

        [Option('d', "domain", Required = false, DefaultValue = false,
             HelpText = "allow downloading of content from different domains, by default not allowed")]
        public bool Domain { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
                (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}