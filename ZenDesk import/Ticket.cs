using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ZenDesk_import
{

   public class Ticket
   {
      public Ticket(XmlElement xmlTicket)
      {
         ticketNumber = int.Parse(xmlTicket["nice-id"].InnerText);
         subject = xmlTicket["subject"].InnerText;
         description = xmlTicket["description"].InnerText;
         creationDate = DateTime.Parse(xmlTicket["created-at"].InnerText);
         closeDate = DateTimeParse.ParseNullableDateTime(xmlTicket["solved-at"].InnerText);
      }

      // Zendesk N/A
      public bool hidden { get; set; }
      // Zendesk nice-id
      public int ticketNumber { get; set; }
      // Zendesk created-at
      public DateTime creationDate { get; set; }
      // Zendesk subject
      public string subject { get; set; }
      // Zendesk description
      public string description { get; set; }
      // Zendesk external-id
      public string referenceNumber { get; set; }
      // Zendesk organization-id
      public string organizationId { get; set; }
      // Zendesk Comments
      public List<History> history { get; set; }
      // Zendesk requester-id
      public string createdByUserId { get; set; }
      // Zendesk N/A
      public string createdByDisplayname { get; set; }
      // Zendesk N/A
      public string remark { get; set; }
      // Zendesk N/A (a custom field that is present in ticket-field-entries[0])
      public string categoryId { get; set; }
      // Zendesk N/A
      public DateTime hideUntilDate { get; set; }
      // Zendesk solved-at
      public DateTime? closeDate { get; set; }
      // Zendesk group-id
      public string assignedDepartmentId { get; set; }
      // Zendesk assignee-id
      public string assignedUserId { get; set; }
      // Zendesk current-tags
      public List<TicketTag> tagsPerTicket { get; set; }

      // Zendesk (a custom field that is present in ticket-field-entries[3])
      public string priorityId { get; set; }
   }

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
      public string ticketId { get; set; }
      // Zendesk N/A
      public string by { get; set; }
      // Zendesk created-at
      public DateTime creationDate { get; set; }
      // Zendesk N/A
      public string message { get; set; }
      // Zendesk value (depending on is-public true)
      public string remark { get; set; }
      // Zendesk value (depending on is-public false)
      public string remarkInternal { get; set; }
      // Zendesk N/A
      public string index { get; set; }
      // Zendesk author-id
      public string userId { get; set; }

      // Zendesk attachment
      public List<Attachment> attachements { get; set; }
   }

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

   public class TicketCategory
   {
      public string id { get; set; }
      public bool hidden { get; set; }
      public string name { get; set; }
   }


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

   public class DateTimeParse
   {
      private static DateTime returnDate;

      public static DateTime? ParseNullableDateTime(string value)
      {
         return DateTime.TryParse(value, out returnDate) ? returnDate : (DateTime?)null;
      }
   }
}
