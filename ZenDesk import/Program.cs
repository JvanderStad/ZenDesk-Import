using System;
using System.Diagnostics;
using System.Linq;
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


			if ( !LoadXml( options.XmlFile ) )
			{
				Logger.Error("Parsing failed");
				return;
			}

	
			Console.ReadLine();
		}

		private static bool LoadXml( string xmlFile )
		{
			Logger.Info( "Loading Xml" );

			var xml = new XmlDocument();
			try
			{
				xml.Load( xmlFile );
			}
			catch ( Exception exception )
			{
				Logger.Error( exception, "Error loading XML: {0}", exception );
				return false;
			}
			Logger.Info("Loading Xml");


			ParseXml(xml);

			return false;
		}

		private static void ParseXml( XmlDocument xml )
		{
			var tickets = xml.SelectNodes( "/" );
			if ( tickets == null )
				return;

			Logger.Info( "{0} tickets found", tickets.Count );
			foreach ( var ticket in tickets.Cast<XmlElement>() )
			{
				
			}
		}
	}
}
