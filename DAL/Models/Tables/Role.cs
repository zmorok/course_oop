using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Tables
{
    [Table("roles", Schema = "core")]
    public class Role
    {
        [Key]
        [Column("id_role")]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        [Column("role_name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("role_privileges", TypeName = "jsonb")]
        public JsonDocument Privileges { get; set; } = null!;

        public ICollection<User> Users { get; set; } = [];
    }
}
