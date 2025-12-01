using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace DAL.Models.Views
{
    public sealed class Counterpart
    {
        [Key][Required]
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = "";
    }
}
