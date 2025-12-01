using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("projects", Schema = "core")]
    public class Project
    {
        [Key]
        [Column("id_project")]
        public int Id { get; set; }

        [Required]
        [Column("id_customer")]
        public int CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public User Customer { get; set; } = null!;

        [Required, MaxLength(200)]
        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }

        public ICollection<Order> Orders { get; set; } = [];
    }
}
