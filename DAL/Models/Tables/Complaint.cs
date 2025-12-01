using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("complaints", Schema = "core")]
    public class Complaint
    {
        [Key]
        [Column("id_complaint")]
        public int Id { get; set; }

        // На кого жалоба
        [Required]
        [Column("id_user")]
        public int UserComId { get; set; }

        [ForeignKey(nameof(UserComId))]
        public User TargetUser { get; set; } = null!;

        // Кто подал
        [Required]
        [Column("filed_by")]
        public int FiledById { get; set; }

        [ForeignKey(nameof(FiledById))]
        public User FiledBy { get; set; } = null!;

        // Кто обработал (администратор)
        [Column("processed_by_admin")]
        public int? ProcessedByAdminId { get; set; }

        [ForeignKey(nameof(ProcessedByAdminId))]
        public User? ProcessedByAdmin { get; set; }

        [Required, MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Required]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }

        // Опциональные связи на активный/архивный заказ (хотя бы одно из полей заполняется)
        [Column("id_order")]
        public int? OrderId { get; set; }          // core.orders.id_order

        [Column("id_order_arc")]
        public int? OrderArchiveId { get; set; }   // core.orders_archive.id_order_arc
    }
}
