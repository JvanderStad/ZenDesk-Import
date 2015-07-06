using System;
using System.Collections.Generic;
using System.Xml;

namespace ZenDesk_import
{
	public class History
	{
		public History(XmlElement xmlComment)
		{
			creationDate = DateTime.Parse(xmlComment["created-at"].InnerText);
			if (Boolean.Parse(xmlComment["is-public"].InnerText))
				remark = xmlComment["value"].InnerText;
			else
				remarkInternal = xmlComment["value"].InnerText;
		}

		// Zendesk N/A
		public string by { get; set; }
		// Zendesk created-at
		public DateTime? creationDate { get; set; }
		// Zendesk N/A
		public string message { get; set; }
		// Zendesk value (depending on is-public true)
		public string remark { get; set; }
		// Zendesk value (depending on is-public false)
		public string remarkInternal { get; set; }
		// Zendesk N/A
		public int index { get; set; }
		// Zendesk author-id
		public string userId { get; set; }

		// Zendesk attachment
		public List<Attachment> attachments = new List<Attachment>();
	}
}