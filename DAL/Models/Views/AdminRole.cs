using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DAL.Models.Views
{
    [Table("v_roles", Schema = "core")]
    public class AdminRole
    {
        [Key]
        [Column("id_role")]
        public int Id { get; set; }

        [Column("role_name")]
        public string? Name { get; set; }

        // Во view нет привилегий — оставим как вычисляемое локально
        [NotMapped]
        public string? Privileges { get; set; }
    }
}
