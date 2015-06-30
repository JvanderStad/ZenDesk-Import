using CommandLine;
using CommandLine.Text;


namespace ZenDesk_import
{
	internal class CommandlineOptions
	{
		[Option("xml", HelpText = "Source Xml file")]
		public string XmlFile { get; set; }


		[HelpOption]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this,
			  current => HelpText.DefaultParsingErrorsHandler(this, current));
		}
	}
}
