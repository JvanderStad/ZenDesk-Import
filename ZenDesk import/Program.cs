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
using System.Security.Cryptography;

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


         if (!LoadXml(options.XmlFile, options))
         {
            Logger.Error("Parsing failed");
            return;
         }


         Console.ReadLine();
      }

      private static bool LoadXml(string xmlFile, CommandlineOptions options)
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


         ParseXml(xml, options);

         CreateJson();

         return false;
      }

      private static void CreateJson()
      {
         Logger.Info("Creating JSON");
         var result = JsonConvert.SerializeObject(_result);

         Logger.Info(result);
      }

      private static void ParseXml(XmlDocument xml, CommandlineOptions options)
      {
         var tickets = xml.SelectNodes("/tickets/ticket");
         if (tickets == null)
            return;

         Logger.Info("{0} tickets found", tickets.Count);
         LoginResult loginResult = CreateLoginRequest(options);


         foreach (var xmlTicket in tickets.Cast<XmlElement>())
         {
            var ticket = new Ticket(xmlTicket);
            ticket.organizationId = GetOrganizationId(loginResult, xmlTicket["organization-id"].InnerText, options); // map to organization id
            ticket.statusId = xmlTicket["status-id"].InnerText; //Map to the right Guid
            ticket.createdByUserId = GetUserId(loginResult, xmlTicket["requester-id"].InnerText, options); // map to the user id

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

      private static LoginResult CreateLoginRequest(CommandlineOptions options)
      {
         var content = JsonConvert.SerializeObject(new Login(options));

         WebRequest request = CreateRequest("login", "POST", options, null);
         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var requestStream = request.GetRequestStream();
         requestStream.Write(dataArray, 0, dataArray.Length);
         requestStream.Close();

         return GetObject<LoginResult>(request);
      }

      private static string GetOrganizationId(LoginResult login, string oldId, CommandlineOptions options)
      {
         WebRequest request = CreateRequest(String.Format("organizations?code={0}", oldId), "GET", options, login);

         Organization organization = GetObject<Organization>(request);

         return organization.id.ToString();
      }

      private static string GetUserId(LoginResult login, string oldId, CommandlineOptions options)
      {
         WebRequest request = CreateRequest(String.Format("user?code={0}", oldId), "GET", options, login);

         User user = GetObject<User>(request);

         return user.id.ToString();
      }

      private static WebRequest CreateRequest(string endpoint, string method, CommandlineOptions options, LoginResult login)
      {
         WebRequest request = WebRequest.Create(options.DefactoUrl + options.ApiRoot + endpoint);
         request.Method = method;

         if (login != null)
         {
            int timestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            string concatenatedString = String.Format("{0}{1}{2}{3}{4}", login.applicationId, method.ToLower(), options.ApiRoot, endpoint, timestamp);
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = encoding.GetBytes(login.secret);
            byte[] messageBytes = encoding.GetBytes(concatenatedString);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
               byte[] hashmessage = hmacsha256.ComputeHash(messageBytes);

               string authHeader = String.Format("hmac256 {0} {1} {2}", login.applicationId, timestamp, ByteToString(hashmessage));
               request.Headers.Add("Authentication", authHeader);
            }
         }

         return request;
      }

      private static T GetObject<T>(WebRequest request)
      {
         WebResponse response = request.GetResponse();

         var dataStream = response.GetResponseStream();
         var reader = new StreamReader(dataStream);
         string json = reader.ReadToEnd();

         T result = (T)JsonConvert.DeserializeObject(json, typeof(T));

         return result;
      }

      private static string ByteToString(byte[] buff)
      {
         string sbinary = "";

         for (int i = 0; i < buff.Length; i++)
         {
            sbinary += buff[i].ToString("X2"); // hex format
         }
         return (sbinary);
      }

      public class Login
      {
         public string username { get; set; }
         public string password { get; set; }
         public string application { get; set; }

         public Login(CommandlineOptions options)
         {
            username = options.ApiUsername;
            password = options.ApiPassword;
            application = options.ApiApplication;
         }
      }

      public class LoginResult
      {
         public bool valid { get; set; }
         public string applicationId { get; set; }
         public string secret { get; set; }
         public bool notValidated { get; set; }
      }

      public class Organization
      {
         public string id { get; set; }
         public bool hidden { get; set; }
         public string code { get; set; }
         public string name { get; set; }
         public string vatNumber { get; set; }
         public string remark { get; set; }
         public string statusId { get; set; }
      }

      
      public class User
      {
         public string id { get; set; }
         public string displayname { get; set; }
         public string email { get; set; }
      }

   }
}
