namespace ZenDesk_import
{
	public class Attachment
	{
		public string id { get; set; }
		public string ticketId { get; set; }
		// Zendesk filename
		public string filename { get; set; }
		// Zendesk N/A
		public string description { get; set; }
		// Zendesk N/A
		public string data { get; set; }
		// Zendesk is-public
		public bool visibleForExternalnet { get; set; }
		// Zendesk N/A
		public bool includeWithMail { get; set; }
	}
}