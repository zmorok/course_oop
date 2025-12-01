using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DAL.Models.Views
{
    public sealed class FreelancerRow
    {
        [Key]
        public int Id { get; init; }
        public string FullName { get; init; } = "";
        public string SkillsPreview { get; init; } = "";
        public bool CanInvite { get; init; }
    }
}
