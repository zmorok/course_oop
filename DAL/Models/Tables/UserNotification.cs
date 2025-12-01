using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Tables
{
    [Keyless]
    [Table("v_user_notifications", Schema = "core")]
    public class UserNotification
    {
        [Key]
        [Column("id_notification")]
        public int Id_Notification { get; set; }

        [Column("id_project")]
        public int Id_Project { get; set; }

        [Column("project_title")]
        public string Project_Title { get; set; } = string.Empty;

        [Column("id_sender")]
        public int Id_Sender { get; set; }

        [Column("sender_name")]
        public string Sender_Name { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime Created_At { get; set; }
    }
}
