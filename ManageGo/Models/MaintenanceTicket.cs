using System;
using System.Collections.Generic;
using PropertyChanged;
using System.Linq;
using Newtonsoft.Json;

namespace ManageGo
{
    [AddINotifyPropertyChangedInterface]
    public class MaintenanceTicket
    {
        public int TicketId { get; set; }
        public string TicketNumber { get; set; }
        //Status false = closed ticket
        [AlsoNotifyFor("TicketStatus")]
        public bool Status { get; set; }
        public string TicketStatus { get; set; }
        public DateTime TicketCreateTime { get; set; }
        public string TicketSubject { get; set; }
        public Tenant Tenant { get; set; }
        public Unit Unit { get; set; }
        public Building Building { get; set; }
        public string FirstComment { get; set; }
        public List<MaintenanceCategory> Categories { get; set; }
        public DateTime? DueDate { get; set; }
        public int NumberOfReplies { get; set; }
        public bool HasWorkorder { get; set; }
        public bool HasEvent { get; set; }
        public bool HasPet { get; set; }
        public bool HasAccess { get; set; }
        public bool NeedReply { get; set; }

        public List<MaintenanceTicketComment> Comments { get; set; }
        [JsonIgnore]
        public string NumberOfRepliesString
        {
            get
            {
                return $"{NumberOfReplies}";
            }
        }
        [JsonIgnore]
        public string CommentImage
        {
            get
            {
                return NumberOfReplies > 0 ? "chat_green.png" : "chat_red.png";
            }
        }
        [JsonIgnore]
        public string FormattedDate
        {
            get
            {
                return TicketCreateTime.ToString("MMM dd - h:mm tt");
            }
        }
        [JsonIgnore]
        public string Category
        {
            get
            {
                return Categories.Count > 0 ? Categories.First().CategoryName : "";
            }
        }
        [JsonIgnore]
        public string TenantDetails
            => Tenant == null ? "" : $"{Tenant.TenantFirstName} {Tenant.TenantLastName}{Environment.NewLine}{Environment.NewLine}{Building?.BuildingShortAddress} #{Unit?.UnitName}";

        [JsonIgnore]
        public bool FirstCommentShown { get; set; }


    }
}
