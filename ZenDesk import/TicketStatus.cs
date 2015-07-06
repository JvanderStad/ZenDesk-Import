using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenDesk_import
{

	public class TicketStatus
	{
		public string id { get; set; }
		public bool hidden { get; set; }
		public string name { get; set; }
		public bool statusNew { get; set; }
		public bool statusDone { get; set; }
		public bool statusHide { get; set; }
		public bool statusReschedule { get; set; }
		public bool statusScheduled { get; set; }
		public bool statusAssignExternal { get; set; }
		public int assignExternalReminderDays { get; set; }
		public bool statusAssignPerson { get; set; }
		public int statusAssignPersonReminderDays { get; set; }
		public string remarkForUser { get; set; }

		public short sortOrder { get; set; }
	}

}
