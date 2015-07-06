namespace ZenDesk_import
{
	internal class Login
	{
		public string username
		{
			get;
			set;
		}

		public string password
		{
			get;
			set;
		}

		public string application
		{
			get;
			set;
		}

		public Login(CommandlineOptions options)
		{
			username = options.ApiUsername;
			password = options.ApiPassword;
			application = options.ApiApplication;
		}
	}
}