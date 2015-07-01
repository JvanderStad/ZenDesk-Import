using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

         Logger.Info("Create Login request");
         var loginResult = CreateLoginRequest(options);

         Dictionary<string, string> localTags = GetTicketTags(loginResult, options);
         Dictionary<string, string> localCategories = GetCategories(loginResult, options);
         Dictionary<string, string> localPriorities = GetPriorities(loginResult, options);

         foreach (var xmlTicket in tickets.Cast<XmlElement>())
         {

            var ticket = new Ticket(xmlTicket);
            Logger.Info("Creating Ticket {0}", ticket.ticketNumber);

            ticket.organizationId = GetOrganizationId(loginResult, xmlTicket["organization-id"].InnerText, options); // map to organization id
            ticket.createdByUserId = GetUserId(loginResult, xmlTicket["requester-id"].InnerText, options); // map to the user id
            ticket.assignedUserId = GetUserId(loginResult, xmlTicket["assignee-id"].InnerText, options); //Map to the user id
            ticket.assignedDepartmentId = GetDepartmentId(loginResult, xmlTicket["group-id"].InnerText, options); //Map to the department id

            // Tags
            ticket.tagsPerTicket = new List<TicketTag>();
            string[] tags = xmlTicket["current-tags"].InnerText.Split(' ');
            foreach (string tag in tags)
            {
               TicketTag ticketTag = new TicketTag(tag);
               if (localTags.ContainsKey(ticketTag.tag))
               {
                  ticketTag.id = localTags[ticketTag.tag];
               }
               else
               {
                  ticketTag = CreateTag(loginResult, ticketTag, options);
                  localTags.Add(ticketTag.tag, ticketTag.id);
               }
               ticket.tagsPerTicket.Add(ticketTag);
            }

            var ticketFields = xmlTicket.SelectNodes("ticket-field-entries/ticket-field-entry");
            // Category
            var category = ticketFields.Cast<XmlElement>().FirstOrDefault();
            if (category != null && !String.IsNullOrWhiteSpace(category["value"].InnerText))
            {
               TicketCategory cat = new TicketCategory();
               cat.name = category["value"].InnerText;
               if (localCategories.ContainsKey(cat.name))
               {
                  ticket.categoryId = localTags[cat.name];
               }
               else
               {
                  cat = CreateCategory(loginResult, cat, options);
                  localCategories.Add(cat.name, cat.id);
                  ticket.categoryId = cat.id;
               }
            }

            // Priority
            var priorityMoscow = ticketFields.Cast<XmlElement>().ElementAt(3).InnerText;
            if (!String.IsNullOrWhiteSpace(priorityMoscow))
            {
               TicketPriority prio = new TicketPriority(priorityMoscow);
               if (localPriorities.ContainsKey(prio.name))
               {
                  ticket.priorityId = localPriorities[prio.name];
               }
               else
               {
                  prio = CreatePriority(loginResult, prio, options);
                  localPriorities.Add(prio.name, prio.id);
                  ticket.priorityId = prio.id;
               }
            }

            // Comments
            ticket.history = new List<History>();
            var comments = xmlTicket.SelectNodes("comments/comment");
            foreach (var xmlComment in comments.Cast<XmlElement>())
            {
               var comment = new History(xmlComment);
               comment.userId = GetUserId(loginResult, xmlComment["author-id"].InnerText, options);
               // Attachments
               comment.attachements = new List<Attachment>();
               var attachments = xmlTicket.SelectNodes("attachments/attachment");
               foreach (var xmlAttachment in attachments.Cast<XmlElement>())
               {
                  WebClient client = new WebClient();
                  byte[] data = client.DownloadData(new Uri(xmlAttachment["url"].InnerText));

                  Attachment attachment = new Attachment();
                  attachment.data = Convert.ToBase64String(data);
                  attachment.filename = xmlAttachment["filename"].InnerText;
                  attachment.visibleForExternalnet = Boolean.Parse(xmlComment["is-public"].InnerText);
               }

               ticket.history.Add(comment);
            }

            _result.Add(ticket);
         }
      }

      private static Dictionary<string, string> GetCategories(LoginResult loginResult, CommandlineOptions options)
      {
         Dictionary<string, string> categories = new Dictionary<string, string>();
         WebRequest request = CreateRequest("ticketTags", "GET", options, loginResult);

         foreach (var cat in GetList<TicketCategory>(request))
         {
            if (!categories.ContainsKey(cat.name))
               categories.Add(cat.name, cat.id);
         }
         return categories;
      }

      private static TicketTag CreateTag(LoginResult loginResult, TicketTag ticketTag, CommandlineOptions options)
      {
         var content = JsonConvert.SerializeObject(ticketTag);

         var request = CreateRequest("ticketTag", "POST", options, loginResult);

         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var requestStream = request.GetRequestStream();
         requestStream.Write(dataArray, 0, dataArray.Length);
         requestStream.Close();

         return GetObject<TicketTag>(request);
      }

      private static Dictionary<string, string> GetPriorities(LoginResult loginResult, CommandlineOptions options)
      {
         Dictionary<string, string> priorities = new Dictionary<string, string>();
         WebRequest request = CreateRequest("ticketPriorities", "GET", options, loginResult);

         foreach (var prio in GetList<TicketPriority>(request))
         {
            if (!priorities.ContainsKey(prio.name))
               priorities.Add(prio.name, prio.id);
         }
         return priorities;
      }

      private static TicketPriority CreatePriority(LoginResult loginResult, TicketPriority ticketPriority, CommandlineOptions options)
      {
         var content = JsonConvert.SerializeObject(ticketPriority);

         var request = CreateRequest("ticketPriority", "POST", options, loginResult);

         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var requestStream = request.GetRequestStream();
         requestStream.Write(dataArray, 0, dataArray.Length);
         requestStream.Close();

         return GetObject<TicketPriority>(request);
      }

      private static TicketCategory CreateCategory(LoginResult loginResult, TicketCategory category, CommandlineOptions options)
      {
         var content = JsonConvert.SerializeObject(category);

         var request = CreateRequest("ticketCategory", "POST", options, loginResult);

         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var requestStream = request.GetRequestStream();
         requestStream.Write(dataArray, 0, dataArray.Length);
         requestStream.Close();

         return GetObject<TicketCategory>(request);
      }

      private static LoginResult CreateLoginRequest(CommandlineOptions options)
      {
         var content = JsonConvert.SerializeObject(new Login(options));

         var request = CreateRequest("login", "POST", options, null);
         var dataArray = Encoding.UTF8.GetBytes(content.ToString());
         request.ContentLength = dataArray.Length;

         var requestStream = request.GetRequestStream();
         requestStream.Write(dataArray, 0, dataArray.Length);
         requestStream.Close();

         return GetObject<LoginResult>(request);
      }

      private static string GetOrganizationId(LoginResult login, string oldId, CommandlineOptions options)
      {
         var request = CreateRequest(String.Format("organizations?searchCode={0}", oldId), "GET", options, login);

         var organization = GetList<Organization>(request).FirstOrDefault();

         if (organization != null)
            return organization.id.ToString();
         else
            return String.Empty;
      }

      private static string GetDepartmentId(LoginResult login, string oldId, CommandlineOptions options)
      {
         var request = CreateRequest(String.Format("departments?extranetName={0}", oldId), "GET", options, login);

         var department = GetList<Department>(request).FirstOrDefault();

         if (department != null)
            return department.id.ToString();
         else
            return String.Empty;
      }

      private static string GetUserId(LoginResult login, string userId, CommandlineOptions options)
      {
         var request = CreateRequest(String.Format("users?registrationNumber={0}", userId), "GET", options, login);

         var user = GetList<User>(request).FirstOrDefault();

         if (user != null)
            return user.id.ToString();
         else
            return GetPersonId(login, userId, options);
      }

      private static string GetPersonId(LoginResult login, string oldId, CommandlineOptions options)
      {
         var request = CreateRequest(String.Format("persons?registrationNumber={0}", oldId), "GET", options, login);

         var person = GetList<Person>(request).FirstOrDefault();

         if (person != null)
            return person.id.ToString();
         else
            return String.Empty;
      }

      private static Dictionary<string, string> GetTicketTags(LoginResult login, CommandlineOptions options)
      {
         Dictionary<string, string> tags = new Dictionary<string, string>();
         WebRequest request = CreateRequest("ticketTags", "GET", options, login);

         foreach (var tag in GetList<TicketTag>(request))
         {
            if (!tags.ContainsKey(tag.tag))
               tags.Add(tag.tag, tag.id);
         }
         return tags;
      }

      public static long GetEpochTime(DateTime dt)
      {
         return
            Convert.ToInt64(
                             (dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                                .TotalMilliseconds);
      }

      private static WebRequest CreateRequest(string endpoint, string method, CommandlineOptions options, LoginResult login)
      {
         var request = WebRequest.Create(options.DefactoUrl + options.ApiRoot + endpoint);
         request.Method = method;

         if (login != null)
         {
            var timestamp = GetEpochTime(DateTime.Now);

            var concatenatedString = String.Format("{0}{1}{2}{3}{4}", login.applicationId, method.ToLower(), options.ApiRoot, endpoint, timestamp);
            var encoding = new ASCIIEncoding();
            var keyByte = encoding.GetBytes(login.secret);
            var messageBytes = encoding.GetBytes(concatenatedString);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
               var hashmessage = hmacsha256.ComputeHash(messageBytes);

               var authHeader = String.Format("hmac256 {0} {1} {2}", login.applicationId, timestamp, ByteToString(hashmessage));
               request.Headers.Add("Authentication", authHeader);
            }
         }

         return request;
      }

      private static T GetObject<T>(WebRequest request)
      {
         WebResponse response;
         try
         {
            response = request.GetResponse();
         }
         catch (WebException e)
         {
            Logger.Info("Could not get object, got {0}", e);
            response = e.Response as WebResponse;
         }


         var dataStream = response.GetResponseStream();
         var reader = new StreamReader(dataStream);
         var json = reader.ReadToEnd();

         var result = JsonConvert.DeserializeObject<T>(json);

         return result;
      }

      private static List<T> GetList<T>(WebRequest request)
      {
         var response = request.GetResponse();

         var dataStream = response.GetResponseStream();
         var reader = new StreamReader(dataStream);
         var json = reader.ReadToEnd();

         var result = JsonConvert.DeserializeObject<List<T>>(json);

         return result;

      }

      private static string ByteToString(byte[] buff)
      {
         var sbinary = "";

         for (var i = 0; i < buff.Length; i++)
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

      public class Department 
      {
         public string id { get; set; }
         public bool hidden { get; set; }
         public string name { get; set; }
         public string extranetName { get; set; }
         public bool useForTickets { get; set; }
      }

      public class User
      {
         public string id { get; set; }
         public string displayname { get; set; }
         public string email { get; set; }
         public string registrationNumber { get; set; }
      }

      public class Person
      {
         public string id { get; set; }
         public string searchCode { get; set; }
         public string registrationNumber { get; set; }
      }
   }
}
