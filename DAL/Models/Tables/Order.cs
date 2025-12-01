using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Tables
{
    [Table("orders", Schema = "core")]
    public class Order
    {
        [Key]
        [Column("id_order")]
        public int Id { get; set; }

        [Required]
        [Column("id_project")]
        public int ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public Project Project { get; set; } = null!;

        [Required]
        [Column("id_freelancer")]
        public int FreelancerId { get; set; }

        [ForeignKey(nameof(FreelancerId))]
        public User Freelancer { get; set; } = null!;

        [Required, MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Required]
        [Column("creation_date")]
        public DateTime CreationDate { get; set; }

        [Column("deadline", TypeName = "date")]
        public DateOnly? Deadline { get; set; }
    }
}
