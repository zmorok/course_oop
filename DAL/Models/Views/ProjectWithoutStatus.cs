using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Views
{
    [Table("v_projects", Schema = "core")]
    public class ProjectWithoutStatus
    {
        [Key]
        [Column("id_project")]
        public int Id_Project { get; set; }

        [Column("id_customer")]
        public int Id_Customer { get; set; }

        [Column("title")]
        public string Title { get; set; } = "";

        [Column("description")]
        public string Description { get; set; } = "";

        [Column("media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }
    }
}
