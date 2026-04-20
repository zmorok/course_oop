using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DAL.Models.Views
{
    public sealed class FreelancerRow
    {
        [Key]
        public int Id { get; init; }
        public string FullName { get; init; } = "";
        public string SkillsPreview { get; init; } = "";
        public bool CanInvite { get; init; }

        [Column("Media", TypeName = "jsonb")]
        public JsonDocument? Media { get; set; }
    }
}
