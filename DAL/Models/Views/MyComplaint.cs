using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [NotMapped]
        public string DescriptionPreview => Description.Length <= 50 ? Description : Description[..50] + "…";

        [NotMapped]
        public bool IsEditable => Status == "new";

        // Новое: сколько осталось изменений (не уходим в минус)
        [NotMapped] public short RemainingChanges => (short)Math.Max(0, 3 - ChangeCounter);

        // Если нужно скрывать/отключать кнопку «Изменить», когда лимит исчерпан:
        [NotMapped] public bool CanEditMore => IsEditable && RemainingChanges > 0;
    }
}
