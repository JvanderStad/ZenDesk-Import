using System;
using System.Collections.Generic;
using System.Xml;

namespace ZenDesk_import
{

   public class Ticket
   {
	   public string personId;

	   public Ticket(XmlElement xmlTicket)
      {
         ticketNumber = int.Parse(xmlTicket["nice-id"].InnerText);
         subject = xmlTicket["subject"].InnerText;
         description = xmlTicket["description"].InnerText;
		 creationDate = DateTimeParse.ParseNullableDateTime(xmlTicket["created-at"].InnerText);
         closeDate = DateTimeParse.ParseNullableDateTime(xmlTicket["solved-at"].InnerText);
		 lastActionDate = DateTimeParse.ParseNullableDateTime(xmlTicket["updated-at"].InnerText);
      }
	  public List<Attachment> attachments = new List<Attachment>();

      // Zendesk N/A
      public bool hidden { get; set; }
      // Zendesk nice-id
      public int ticketNumber { get; set; }
      // Zendesk created-at
      public DateTime? creationDate { get; set; }
      // Zendesk subject
      public string subject { get; set; }
      // Zendesk description
      public string description { get; set; }
      // Zendesk external-id
      public string referenceNumber { get; set; }
      // Zendesk organization-id
      public string organizationId { get; set; }
      // Zendesk Comments
      public List<History> history = new List<History>();
	  public DateTime? lastActionDate;
	   // Zendesk requester-id
      public string createdByUserId { get; set; }
      // Zendesk N/A
      public string createdByDisplayname { get; set; }
      // Zendesk N/A
      public string remark { get; set; }
      // Zendesk N/A (a custom field that is present in ticket-field-entries[0])
      public string categoryId { get; set; }
      // Zendesk N/A
      public DateTime? hideUntilDate { get; set; }
      // Zendesk solved-at
      public DateTime? closeDate { get; set; }
      // Zendesk group-id
      public string assignedDepartmentId { get; set; }
      // Zendesk assignee-id
      public string assignedUserId { get; set; }
      // Zendesk current-tags
	  public List<TagsPerTicket> tagsPerTicket { get; set; }

      // Zendesk (a custom field that is present in ticket-field-entries[3])
      public string priorityId { get; set; }

	  /// <summary>
	  /// Gets or sets the status identifier.
	  /// </summary>
	  /// <value>The status identifier.</value>
	  public string statusId { get; set; }
   }
}
