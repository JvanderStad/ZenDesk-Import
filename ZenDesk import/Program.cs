using System;
using System.Collections.Generic;
using System.Globalization;
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
	internal static class Program
	{
		private static CommandlineOptions _options;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private static readonly XmlDocument Xml = new XmlDocument();
		private static readonly List<Ticket> Result = new List<Ticket>();
		
		private static LoginResult _loginCredentials;

		private static readonly Dictionary<string, TicketTag> Tags = new Dictionary<string, TicketTag>();
		private static readonly Dictionary<string, TicketCategory> Categories = new Dictionary<string, TicketCategory>();
		private static readonly Dictionary<string, TicketStatus> Statuses = new Dictionary<string, TicketStatus>();
		private static readonly Dictionary<string, TicketPriority> Priorities = new Dictionary<string, TicketPriority>();

		private static void Main( string[] args )
		{
			Logger.Info( "Starting application" );
		
			//check if arguments are valid
			if (!ValidateArguments(args))
				return;

			//create temp folders
			CreateRequiredFolders();
			
			//can we login?
			ValidateCredentials();

			//load the zendesk xml
			if (!LoadXml())
				return;

			//preload data from api
			if ( !PreloadData() )
				return;
		
			//parse the xml
			ParseXml();

			//post the tickets
			PostTickets();
		}


		private static bool ValidateArguments(string[] args)
		{
			_options = new CommandlineOptions();
			var arguments = Parser.Default.ParseArguments(args, _options);
			if ( arguments )
				return true;

			Logger.Error("Error parsing parameters: {0}", String.Join(", ", args));
			return false;
		}

		private static void CreateRequiredFolders()
		{
			if ( !Directory.Exists( "files" ) )
				Directory.CreateDirectory( "files" );

			_fileCache = Directory.GetCurrentDirectory() + "\\files";
		}

		private static bool LoadXml()
		{
			if (String.IsNullOrEmpty(_options.XmlFile))
			{
				Logger.Error("Xml file not set");
				return false;
			}

			Logger.Trace( "Loading Xml" );
			
			try
			{
				Xml.Load(_options.XmlFile);
			}
			catch ( Exception exception )
			{
				Logger.Error( exception, "Error loading XML: {0}", exception );
				return false;
			}

			
			return true;
		}

		private static void PostTickets()
		{
			var splitIndex = 0;
			foreach (var split in Split(Result, 25))
			{
				splitIndex += split.Count;

				Logger.Info( "Creating tickets: {0}/{1} ({2}%)",

				             splitIndex,
				             Result.Count,

							 ((splitIndex / (decimal)Result.Count) * 100.0m).ToString("n", CultureInfo.InvariantCulture)

					);

				var content = JsonConvert.SerializeObject(split, new JsonSerializerSettings()
				{
					NullValueHandling = NullValueHandling.Ignore
				});

				var request = CreateRequest("tickets", HttpMethod.Post);
				var dataArray = Encoding.UTF8.GetBytes(content);
				request.ContentLength = dataArray.Length;

				using ( var requestStream = request.GetRequestStream() )
				{
					requestStream.Write( dataArray, 0, dataArray.Length );
					requestStream.Close();
				}

				var response = (HttpWebResponse)request.GetResponse();
				if ( response.StatusCode != HttpStatusCode.OK )
				{
					Logger.Warn( "Result was not ok: {0}", response.StatusDescription );
				}
				else
				{
					Logger.Trace( "Insert was ok" );
				}
			}
		}

		private static IEnumerable<List<Ticket>> Split( IEnumerable<Ticket> source, int count )
		{
			return source
				.Select( ( x, i ) => new
				{
					Index = i,
					Value = x
				} )
				.GroupBy( x => x.Index / count )
				.Select( x => x.Select( v => v.Value ).ToList() )
				.ToList();
		}

		private static void ParseXml()
		{
			Logger.Trace( "Parsing xml" );

			var tickets = Xml.SelectNodes("/tickets/ticket");
			if ( tickets == null )
				return;

			Logger.Debug( "{0} tickets found in xml", tickets.Count );

			

			

			var currentTicket = 0;
			foreach ( var xmlTicket in tickets.Cast<XmlElement>() )
			{
				currentTicket++;

				var ticket = new Ticket( xmlTicket );

				Logger.Info("Parsing ticket: {0}/{1} ({2}%)",

							 currentTicket,
							 tickets.Count,

							 ((currentTicket / (decimal)tickets.Count) * 100.0m).ToString("n", CultureInfo.InvariantCulture)

					);

				ticket.organizationId = GetOrganizationId( xmlTicket["organization-id"].InnerText );
					// map to organization id
				ticket.createdByUserId = GetUserId( xmlTicket["requester-id"].InnerText ); // map to the user id
				if ( ticket.createdByUserId == null )
				{
					var id = xmlTicket["requester-id"].InnerText;
					var person = GetPerson(id);
					if ( person != null )
					{
						ticket.personId = person.id;
						ticket.createdByDisplayname = person.displayName;
					}
				}

				ticket.assignedUserId = GetUserId( xmlTicket["assignee-id"].InnerText ); //Map to the user id
				ticket.assignedDepartmentId = GetDepartmentId( xmlTicket["group-id"].InnerText );
					//Map to the department id

				// Tags
				ticket.tagsPerTicket = new List<TagsPerTicket>();
				var tags = xmlTicket["current-tags"].InnerText.Split( new[]
				{
					' '
				},
				                                                      StringSplitOptions.RemoveEmptyEntries );
				foreach ( var tag in tags.Select( x=>x.Replace( "_", " " ) ) )
				{
					var ticketTag = new TagsPerTicket();
					if ( Tags.ContainsKey( tag ) )
					{
						ticketTag.tagId = Tags[tag].id;
					}
					else
					{
						var created = CreateTag( new TicketTag(tag) );
						ticketTag.tagId = created.id;
					}
					ticket.tagsPerTicket.Add( ticketTag );
				}

				var ticketFields = xmlTicket.SelectNodes( "ticket-field-entries/ticket-field-entry" );
				// Category
				var category = ticketFields.Cast<XmlElement>().FirstOrDefault();
				if ( category != null && !String.IsNullOrWhiteSpace( category["value"].InnerText ) )
				{
					var cat = new TicketCategory
					{
						name = category["value"].InnerText
					};
					if (Categories.ContainsKey(cat.name))
					{
						ticket.categoryId = Categories[cat.name].id;
					}
					else
					{
						var ticketCategory = CreateCategory( cat );
						ticket.categoryId = ticketCategory.id;
					}
				}

				// Priority
				if ( ticketFields.Count > 3 )
				{
					var priorityMoscow = ticketFields.Cast<XmlElement>().ElementAt( 3 ).InnerText;
					if ( !String.IsNullOrWhiteSpace( priorityMoscow ) )
					{
						var prio = new TicketPriority( priorityMoscow );
						if ( Priorities.ContainsKey( prio.name ) )
						{
							ticket.priorityId = Priorities[prio.name].id;
						}
						else
						{
							prio = CreatePriority( prio );
							Priorities.Add(prio.name, prio);
							ticket.priorityId = prio.id;
						}
					}
				}

				HandleHistory( ticket, xmlTicket );

				//set status of ticket (if solved, status done)
				var solvedAt = DateTimeParse.ParseNullableDateTime(xmlTicket["solved-at"].InnerText);
				ticket.statusId = solvedAt.HasValue ? StatusDone.id : StatusNew.id;


				Result.Add( ticket );
			}
		}

		private static void HandleHistory( Ticket ticket, XmlNode xmlTicket )
		{
			var comments = xmlTicket.SelectNodes( "comments/comment" );
			foreach ( var comment in comments.Cast<XmlElement>() )
			{
				var history = new History( comment )
				{
					userId = GetUserId( comment["author-id"].InnerText ),
				};

				if ( String.IsNullOrEmpty( history.userId ) )
				{
					var person = GetPerson( comment["author-id"].InnerText );
					if ( person != null )
						history.by = person.displayName;
					
					history.by = "Onbekend";
				}

				//creation date
				history.creationDate = DateTimeParse.ParseNullableDateTime( comment["created-at"].InnerText );

				// Attachments
				var attachments = comment.SelectNodes( "attachments/attachment" );
				foreach ( var xmlAttachment in attachments.Cast<XmlElement>() )
				{
					var uri = new Uri( xmlAttachment["url"].InnerText );


					var hash = GetSha1HashString( uri.ToString() );

					var cache = Path.Combine( _fileCache, hash );
					byte[] data;
					if ( !File.Exists( cache ) )
					{
						Logger.Debug( "Downloading file: {0}", uri );

						var client = new WebClient();
						data = client.DownloadData( uri );

						Logger.Debug( "Writing file to cache: {0}", cache );
						File.WriteAllBytes( cache, data );
					}
					else
					{
						Logger.Trace( "Retreiving file from cache: {0}", uri );
						data = File.ReadAllBytes( cache );
					}

					var attachment = new Attachment
					{
						data = Convert.ToBase64String( data ),
						filename = xmlAttachment["filename"].InnerText,
						visibleForExternalnet = Boolean.Parse( comment["is-public"].InnerText )
					};
					ticket.attachments.Add( attachment );
				}

				ticket.history.Add( history );
			}

			//set history index
			var index = 1;
			foreach (var history in ticket.history.OrderBy(x => x.creationDate))
				history.index = index++;
		}

		/// <summary>
		/// Preloads the data.
		/// </summary>
		private static bool PreloadData()
		{
			LoadTags();
			LoadCategories();
			if ( !LoadStatuses() )
				return false;
			LoadPriorities();
			
			return true;
		}

		private static bool LoadStatuses()
		{
			Logger.Trace("Retreiving ticket statuses");

			var result = RequestList<TicketStatus>("ticketStatuses");
			foreach (var status in result)
				Statuses[status.name] = status;


			StatusNew = Statuses.Values
				.Where( status => status.statusNew )
				.OrderBy( status => status.sortOrder )
				.FirstOrDefault();

			if ( StatusNew == null )
			{
				Logger.Error( "No status new found" );
				return false;
			}

			StatusDone = Statuses.Values
				.Where(status => status.statusDone)
				.OrderBy(status => status.sortOrder)
				.FirstOrDefault();

			if (StatusDone == null)
			{
				Logger.Error("No status new found");
				return false;
			}

			return true;
		}

		public static TicketStatus StatusDone
		{
			get;
			set;
		}

		public static TicketStatus StatusNew
		{
			get;
			set;
		}

		private static void LoadCategories()
		{
			Logger.Trace("Retreiving ticket categories");

			var result = RequestList<TicketCategory>("ticketCategory");
			foreach (var category in result)
				Categories[category.name] = category;
		}

		private static IEnumerable<byte> GetSha1Hash(string inputString)
		{
			using (var algorithm = SHA1.Create())
			{
				return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
			}
		}

		public static string GetSha1HashString(string inputString)
		{
			var sb = new StringBuilder();
			foreach (var b in GetSha1Hash(inputString))
				sb.Append(b.ToString("X2"));

			return sb.ToString();
		}

		private static List<T> RequestList<T>( string endpoint )
		{
			var totalResult = new List<T>();
			var page = 0;
			const int perPage = 10;
			while ( true )
			{
				page++;
				Logger.Trace("Retreiving endpoint {0} ({1})", endpoint, page);

				var request = CreateRequest(endpoint, HttpMethod.Get, page, perPage);
				var json = GetJson(request);

				var result = JsonConvert.DeserializeObject<List<T>>( json );
				if (result.Count == perPage+1)
				{
					totalResult.AddRange(result.Take(perPage));
				}
				else
				{
					totalResult.AddRange( result );
					break;
				}
			}

			return totalResult;
		}

		private static TicketTag CreateTag( TicketTag ticketTag )
		{
			var content = JsonConvert.SerializeObject( ticketTag );
			var request = CreateRequest( "ticketTag", HttpMethod.Post );

			var dataArray = Encoding.UTF8.GetBytes( content );
			request.ContentLength = dataArray.Length;

			var requestStream = request.GetRequestStream();
			requestStream.Write( dataArray, 0, dataArray.Length );
			requestStream.Close();

			var result = GetObject<TicketTag>( request );
			Tags.Add(result.tag, result);

			return result;
		}

		private static void LoadPriorities()
		{
			Logger.Trace("Retreiving ticket priorities");

			var result = RequestList<TicketPriority>("ticketPriorities");
			foreach (var priority in result)
				Priorities[priority.name] = priority;
		}

		private static TicketPriority CreatePriority( TicketPriority ticketPriority )
		{
			var content = JsonConvert.SerializeObject( ticketPriority );

			var request = CreateRequest( "ticketPriority", HttpMethod.Post);

			var dataArray = Encoding.UTF8.GetBytes( content );
			request.ContentLength = dataArray.Length;

			var requestStream = request.GetRequestStream();
			requestStream.Write( dataArray, 0, dataArray.Length );
			requestStream.Close();

			return GetObject<TicketPriority>( request );
		}

		private static TicketCategory CreateCategory( TicketCategory category )
		{
			Logger.Trace( "Creating category: {0}", category.name );

			var content = JsonConvert.SerializeObject( category );

			var request = CreateRequest( "ticketCategory", HttpMethod.Post);

			var dataArray = Encoding.UTF8.GetBytes( content );
			request.ContentLength = dataArray.Length;

			var requestStream = request.GetRequestStream();
			requestStream.Write( dataArray, 0, dataArray.Length );
			requestStream.Close();


			var result = GetObject<TicketCategory>(request);
			Categories.Add(result.name, result);

			return result;
		}

		private static void ValidateCredentials()
		{
			Logger.Info("Validating credentials (login)");

			var content = JsonConvert.SerializeObject( new Login(_options) );

			var request = CreateRequest( "login", HttpMethod.Post);
			var dataArray = Encoding.UTF8.GetBytes( content );
			request.ContentLength = dataArray.Length;

			var requestStream = request.GetRequestStream();
			requestStream.Write( dataArray, 0, dataArray.Length );
			requestStream.Close();

			_loginCredentials = GetObject<LoginResult>(request);
		}


		static readonly Dictionary<string, Organization> OrganizationDictionary = new Dictionary<string, Organization>();
		private static string GetOrganizationId( string organizationId )
		{
			if (OrganizationDictionary.ContainsKey(organizationId))
			{
				var departmentValue = OrganizationDictionary[organizationId];
				return departmentValue == null ? null : departmentValue.id;
			}

			var request = CreateRequest( String.Format( "organizations?searchCode={0}", organizationId ), HttpMethod.Get);
			var organization = GetList<Organization>( request ).FirstOrDefault();
			OrganizationDictionary[organizationId] = organization;
			Logger.Trace("Retreiving id for organization: {0}, id: {1}", organizationId, organization == null ? "Not found" : organization.id);

			return organization != null ? organization.id : null;
		}

		static readonly Dictionary<string, Department> DepartmentDictionary = new Dictionary<string, Department>();
		private static string GetDepartmentId( string departmentId )
		{
			if (DepartmentDictionary.ContainsKey(departmentId))
			{
				var departmentValue = DepartmentDictionary[departmentId];
				return departmentValue == null ? null : departmentValue.id;
			}

			var request = CreateRequest(String.Format("departments?extranetName={0}", departmentId), HttpMethod.Get);
			var department = GetList<Department>( request ).FirstOrDefault();

			DepartmentDictionary[departmentId] = department;
			Logger.Trace("Retreiving id for department: {0}, id: {1}", departmentId, department == null ? "Not found" : department.id);

			return department != null ? department.id : null;
		}

		//local cache for users
		static readonly Dictionary<string, User> UserDictionary = new Dictionary<string, User>();
		private static string GetUserId( string userId )
		{
			if (UserDictionary.ContainsKey(userId))
			{
				var userValue = UserDictionary[userId];
				return userValue == null ? null : userValue.id;
			}
			

			var request = CreateRequest( String.Format( "users?registrationNumber={0}", userId ), HttpMethod.Get);

			var user = GetList<User>( request ).FirstOrDefault();

			UserDictionary[userId] = user;
			Logger.Trace( "Retreiving id for user: {0}, id: {1}", userId, user == null ? "Not found" : user.id );

			return user != null ? user.id : null;
		}


		//local cache for persons
		static readonly Dictionary<string, Person> PersonDictionary = new Dictionary<string, Person>();
		private static string _fileCache;

		private static Person GetPerson(string personId)
		{
			if ( PersonDictionary.ContainsKey( personId ) )
			{
				return PersonDictionary[personId];
			}

			var request = CreateRequest( String.Format( "persons?registrationNumber={0}", personId ), HttpMethod.Get );
			var person = GetList<Person>( request ).FirstOrDefault();

			PersonDictionary[personId] = person;
			Logger.Trace("Retreiving id for person: {0}, id: {1}", personId, person == null ? "Not found" : person.id);

			return person;
		}




		private static void LoadTags()
		{
			Logger.Trace( "Retreiving ticket tags" );

			var result = RequestList<TicketTag>("ticketTags");
			foreach ( var tag in result )
				Tags[tag.tag] = tag;
		}

		private static long GetEpochTime( DateTime dt )
		{
			return
				Convert.ToInt64(
				                ( dt.ToUniversalTime() - new DateTime( 1970, 1, 1, 0, 0, 0, DateTimeKind.Utc ) )
					                .TotalMilliseconds );
		}

		

		private static WebRequest CreateRequest( string endpoint, HttpMethod method, int page = 1, int perPage = 10 )
		{
			var append = endpoint.IndexOf("?", StringComparison.Ordinal) == -1 ? '?' : '&';
			string url;
			switch ( method )
			{
				case HttpMethod.Post:
					url = endpoint;
					break;

				case HttpMethod.Get:
					url = String.Format("{0}{3}page={1}&limit={2}", endpoint, page, perPage, append);
					break;

				default:
					throw new ArgumentOutOfRangeException( "method" );
			}
			
			var request = WebRequest.Create(_options.DefactoUrl + _options.ApiRoot + url);
			request.Method = method.ToString();
			var timestamp = GetEpochTime( DateTime.Now );

			if ( _loginCredentials == null )
				return request;

			var concatenatedString = String.Format( "{0}{1}{2}{3}{4}",
			                                        _loginCredentials == null ? "" : _loginCredentials.applicationId,
			                                        method.ToString().ToLower(),
			                                        _options.ApiRoot,
													url,
			                                        timestamp );

			var encoding = new ASCIIEncoding();
			var keyByte = encoding.GetBytes( _loginCredentials.secret );
			var messageBytes = encoding.GetBytes( concatenatedString );
			using ( var hmacsha256 = new HMACSHA256( keyByte ) )
			{
				var hashmessage = hmacsha256.ComputeHash( messageBytes );

				var authHeader = String.Format( "hmac256 {0} {1} {2}",
				                                _loginCredentials.applicationId,
				                                timestamp,
				                                ByteToString( hashmessage ) );
				request.Headers.Add( "Authentication", authHeader );
			}

			return request;
		}

		private static T GetObject<T>( WebRequest request )
		{
			WebResponse response;
			try
			{
				response = request.GetResponse();
			}
			catch ( WebException e )
			{
				Logger.Info( "Could not get object, got {0}", e );
				response = e.Response;
			}


			var dataStream = response.GetResponseStream();
			if ( dataStream == null )
				return default( T );

			var reader = new StreamReader( dataStream );
			var json = reader.ReadToEnd();

			var result = JsonConvert.DeserializeObject<T>( json );

			return result;
		}

		private static List<T> GetList<T>( WebRequest request )
		{
			var json = GetJson( request );

			var result = JsonConvert.DeserializeObject<List<T>>( json );

			return result;

		}

		private static string GetJson( WebRequest request )
		{
			var response = request.GetResponse();

			var dataStream = response.GetResponseStream();
			if ( dataStream == null )
				return null;

			var reader = new StreamReader( dataStream );
			var json = reader.ReadToEnd();
			return json;
		}

		private static string ByteToString( IEnumerable<byte> buff )
		{
			return buff.Aggregate( "", ( current, t ) => current + t.ToString( "X2" ) );
		}
	}
}
