namespace ZenDesk_import
{
	internal class LoginResult
	{
		public bool valid
		{
			get;
			set;
		}

		public string applicationId
		{
			get;
			set;
		}

		public string secret
		{
			get;
			set;
		}

		public bool notValidated
		{
			get;
			set;
		}
	}
}