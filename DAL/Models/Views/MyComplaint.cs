using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Views
{
    public sealed class MyComplaint
    {
        [Key]
        public int Id_Complaint { get; set; }
        public int Id_User { get; set; }
        public int Filed_By { get; set; }
        public string TargetName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";

        [Column("ChangeCounter", TypeName = "smallint")]
        public short ChangeCounter { get; set; }

        public int? ProcessedAdminId { get; set; }

        [Column("Media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }

        [NotMapped] public string DescriptionPreview => Description.Length <= 50 ? Description : Description[..50] + "…";
        [NotMapped] public bool IsEditable => Status == "new";
        [NotMapped] public short RemainingChanges => Status == "resolved" ? (short)0 : (short)Math.Max(0, 3 - ChangeCounter);
        [NotMapped] public bool CanEditMore => IsEditable && RemainingChanges > 0;
    }
}
