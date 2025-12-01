using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("portfolio", Schema = "core")]
    public class Portfolio
    {
        [Key]
        [Column("id_portfolio")]
        public int Id { get; set; }

        [Required]
        [Column("id_user")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }

        [Required]
        [Column("skills")]
        public string[] Skills { get; set; } = Array.Empty<string>();

        [Column("experience")]
        public string? Experience { get; set; }
    }
}
