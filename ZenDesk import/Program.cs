using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using CommandLine;
using Newtonsoft.Json;
using NLog;

namespace ZenDesk_import
{
	static class Program
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static List<Ticket> _result;

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

			_result = new List<Ticket>();
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

			CreateJson();

			return false;
		}

		private static void CreateJson()
		{
			Logger.Info( "Creating JSON" );
			var result = JsonConvert.SerializeObject( _result );

			Logger.Info( result );
		}

		private static void ParseXml( XmlDocument xml )
		{
			var tickets = xml.SelectNodes( "/" );
			if ( tickets == null )
				return;

			Logger.Info( "{0} tickets found", tickets.Count );
		

			foreach ( var xmlTicket in tickets.Cast<XmlElement>() )
			{
				var ticket = new Ticket();
				_result.Add( ticket );
			}
		}
	}
}
