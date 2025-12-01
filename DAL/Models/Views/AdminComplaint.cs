using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Views
{
    [Table("v_admin_complaints", Schema = "core")]
    public sealed class AdminComplaint
    {
        [Key] [Column("id_complaint")]
        public int Id_Complaint { get; set; }
        public int UserComId { get; set; }
        public string UserComName { get; set; } = "";
        public int FiledById { get; set; }
        public string FiledByName { get; set; } = "";

        [Column("ProcessedByAdminId")]
        public int? AdminId { get; set; }

        [Column("ProcessedByAdminName")]
        public string? AdminName { get; set; }

        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Media { get; set; }
        [Column("id_order")]
        public int? Id_Order { get; set; }
        [Column("id_order_arc")]
        public int? Id_Order_Arc { get; set; }
    }
}
