namespace ZenDesk_import
{
	public class TicketPriority
	{
		public TicketPriority() { }

		public TicketPriority(string name)
		{
			this.name = name.Replace("_", " ");
		}
		public string id { get; set; }
		public bool hidden { get; set; }
		public string name { get; set; }
	}
}