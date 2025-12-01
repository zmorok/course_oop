using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Tables
{
    [Table("users", Schema = "core")]
    public class User
    {
        [Key]
        [Column("id_user")]
        public int Id { get; set; }

        [Required, StringLength(128)]
        [Column("password", TypeName = "char(128)")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Column("role")]
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        [Required, MaxLength(100)]
        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [MaxLength(10)]
        [Column("gender")]
        public string? Gender { get; set; }

        [MaxLength(20)]
        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Required, MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("registration_date")]
        public DateTime RegistrationDate { get; set; }

        [Column("last_online_time")]
        public DateTime? LastOnlineTime { get; set; }

        [Column("rating", TypeName = "decimal(2,1)")]
        public decimal Rating { get; set; }

        [Column("photo")]
        public byte[]? Photo { get; set; }

        // Навигации
        [InverseProperty(nameof(Project.Customer))]
        public ICollection<Project> ProjectsAsCustomer { get; set; } = [];

        [InverseProperty(nameof(Review.Author))]
        public ICollection<Review> ReviewsAuthored { get; set; } = [];

        [InverseProperty(nameof(Review.Recipient))]
        public ICollection<Review> ReviewsReceived { get; set; } = [];

        [InverseProperty(nameof(Order.Freelancer))]
        public ICollection<Order> OrdersAsFreelancer { get; set; } = [];

        // Жалобы: на меня / поданные мной / обработанные мной (как админ)
        [InverseProperty(nameof(Complaint.TargetUser))]
        public ICollection<Complaint> ComplaintsAgainstMe { get; set; } = [];

        [InverseProperty(nameof(Complaint.FiledBy))]
        public ICollection<Complaint> ComplaintsFiledByMe { get; set; } = [];

        [InverseProperty(nameof(Complaint.ProcessedByAdmin))]
        public ICollection<Complaint> ComplaintsProcessedByMe { get; set; } = [];

        // Портфолио у пользователя — несколько записей
        [InverseProperty(nameof(Portfolio.User))]
        public ICollection<Portfolio> Portfolios { get; set; } = [];

        [InverseProperty(nameof(AuditLog.User))]
        public ICollection<AuditLog> AuditLogs { get; set; } = [];
    }
}
