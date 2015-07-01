using CommandLine;
using CommandLine.Text;


namespace ZenDesk_import
{
	internal class CommandlineOptions
	{
		[Option("xml", HelpText = "Source Xml file")]
		public string XmlFile { get; set; }

      [Option("defactoUrl", HelpText = "Root url of API")]
      public string DefactoUrl { get; set; }
      
      [Option("apiRoot", HelpText = "Root url of API")]
      public string ApiRoot { get; set; }

      [Option("username", HelpText = "API login username")]
      public string ApiUsername { get; set; }

      [Option("password", HelpText = "API login password")]
      public string ApiPassword { get; set; }

      [Option("application", HelpText = "API login application")]
      public string ApiApplication { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this,
			  current => HelpText.DefaultParsingErrorsHandler(this, current));
		}
	}
}
