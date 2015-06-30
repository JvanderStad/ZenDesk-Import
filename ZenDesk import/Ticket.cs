using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenDesk_import
{

	public class Ticket
	{
		public bool hidden { get; set; }
		public int ticketNumber { get; set; }
		public string referenceNumber { get; set; }
		public string subject { get; set; }
		public string description { get; set; }
		public string remark { get; set; }
		public string creationDate { get; set; }
		public string closeDate { get; set; }
		public string hideUntilDate { get; set; }
		public string organizationId { get; set; }
		public string categoryId { get; set; }
		public string createdByUserId { get; set; }
		public string createdByDisplayname { get; set; }
		public string statusId { get; set; }
		public List<History> history { get; set; }
	}

	public class History
	{
		public string ticketId { get; set; }
		public string by { get; set; }
		public string message { get; set; }
		public string remark { get; set; }
		public string remarkInternal { get; set; }
		public string index { get; set; }
		public string userId { get; set; }
	}

}
