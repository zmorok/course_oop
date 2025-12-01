using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("reviews", Schema = "core")]
    public class Review
    {
        [Key]
        [Column("id_review")]
        public int Id { get; set; }

        // FK на архивный заказ
        [Required]
        [Column("id_order")]
        public int OrderArchiveId { get; set; }

        [Required]
        [Column("id_author")]
        public int AuthorId { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public User Author { get; set; } = null!;

        [Column("id_recipient")]
        public int? RecipientId { get; set; }

        [ForeignKey(nameof(RecipientId))]
        public User? Recipient { get; set; }

        [Required]
        [Column("comment")]
        public string Comment { get; set; } = string.Empty;

        [Required]
        [Column("rating")]
        public int Rating { get; set; }

        [Column("media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }
    }
}
