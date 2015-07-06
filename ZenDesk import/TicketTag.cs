namespace ZenDesk_import
{
	public class TicketTag
	{
		public string id { get; set; }
		public string tag { get; set; }

		public TicketTag() { }

		public TicketTag(string tag)
		{
			this.tag = tag.Replace("_", " ");
		}
	}

	public class TagsPerTicket
	{
		public string tagId { get; set; }

	}
}