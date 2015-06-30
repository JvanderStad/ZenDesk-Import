using System;
using System.Xml;
using CommandLine;
using NLog;

namespace ZenDesk_import
{
	static class Program
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		static void Main(string[] args)
		{
			Logger.Info( "Starting application" );
			var options = new CommandlineOptions();
			var arguments = Parser.Default.ParseArguments(args, options);
			if (!arguments)
			{
				Logger.Error("Error parsing parameters: {0}", String.Join(", ", args));
				return;
			}

			if ( String.IsNullOrEmpty( options.XmlFile ) )
			{
				Logger.Error("Xml file not set");
				return;
			}


			LoadXml(options.XmlFile);

	
			Console.ReadLine();
		}

		private static void LoadXml( string xmlFile )
		{
			Logger.Info( "Loading Xml" );

			var xml = new XmlDocument();
			xml.Load( xmlFile );
		}
	}
}
