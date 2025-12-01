using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("audit_logs", Schema = "core")]
    public class AuditLog
    {
        [Key]
        [Column("id_log")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [MaxLength(100)]
        [Column("proc_name")]
        public string? ProcName { get; set; }

        [Required, MaxLength(10)]
        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [Column("table_name")]
        public string TableName { get; set; } = string.Empty;

        [Required]
        [Column("record_id")]
        public int RecordId { get; set; }

        [Column("old_data", TypeName = "jsonb")]
        public JsonDocument? OldData { get; set; }

        [Column("new_data", TypeName = "jsonb")]
        public JsonDocument? NewData { get; set; }

        [Required]
        [Column("changed_at")]
        public DateTime ChangedAt { get; set; }
    }
}
