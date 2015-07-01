using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using CommandLine;
using Newtonsoft.Json;
using NLog;
using System.Net;
using System.IO;
using System.Text;

namespace ZenDesk_import
{
   static class Program
   {
      private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
      private static List<Ticket> _result;

      static void Main(string[] args)
      {
         Logger.Info("Starting application");
         var options = new CommandlineOptions();
         var arguments = Parser.Default.ParseArguments(args, options);
         if (!arguments)
         {
            Logger.Error("Error parsing parameters: {0}", String.Join(", ", args));
            return;
         }

         if (String.IsNullOrEmpty(options.XmlFile))
         {
            Logger.Error("Xml file not set");
            return;
         }


         if (!LoadXml(options.XmlFile))
         {
            Logger.Error("Parsing failed");
            return;
         }


         Console.ReadLine();
      }

      private static bool LoadXml(string xmlFile)
      {
         Logger.Info("Loading Xml");

         _result = new List<Ticket>();
         var xml = new XmlDocument();
         try
         {
            xml.Load(xmlFile);
         }
         catch (Exception exception)
         {
            Logger.Error(exception, "Error loading XML: {0}", exception);
            return false;
         }
         Logger.Info("Loading Xml");


         ParseXml(xml);

         CreateJson();

         return false;
      }

      private static void CreateJson()
      {
         Logger.Info("Creating JSON");
         var result = JsonConvert.SerializeObject(_result);

         Logger.Info(result);
      }

      private static void ParseXml(XmlDocument xml)
      {
         var tickets = xml.SelectNodes("/tickets/ticket");
         if (tickets == null)
            return;

         Logger.Info("{0} tickets found", tickets.Count);


         foreach (var xmlTicket in tickets.Cast<XmlElement>())
         {
            var ticket = new Ticket(xmlTicket);
            ticket.organizationId = xmlTicket["organization-id"].InnerText; // map to right Guid
            ticket.statusId = xmlTicket["status-id"].InnerText; //Map to the right Guid
            ticket.createdByUserId = xmlTicket["requester-id"].InnerText; // map to the right Guid

            // Category
            XmlNodeList ticketFields = xmlTicket.SelectNodes("ticket-field-entries/ticket-field-entry");
            var category = ticketFields.Cast<XmlElement>().FirstOrDefault();
            ticket.categoryId = category != null ? category["value"].InnerText : "";

            // Comments
            ticket.history = new List<History>();
            var comments = xmlTicket.SelectNodes("comments/comment");
            foreach (var xmlComment in comments.Cast<XmlElement>())
            {
               var comment = new History(xmlComment);
               ticket.history.Add(comment);
            }

            _result.Add(ticket);
         }
      }

      private static void MakeLoginRequest()
      {
         var options = new CommandlineOptions();
         var content = JsonConvert.SerializeObject(new Login());

         string loginEndpoint = "/login";
         WebRequest request = WebRequest.Create(options.ApiRootUrl+loginEndpoint);
         request.Method = "POST";
         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var data = request.GetRequestStream();
         var reader = new StreamReader(data);
         string json = reader.ReadToEnd();
         
      }
   }

   public class Login
   {
      public string username { get; set; }
      public string password { get; set; }
      public string application { get; set; }

      public Login()
      {

      }
   }
}
