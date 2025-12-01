using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Views
{
    [Table("v_order_extended", Schema = "core")]
    public class LocalOrderDisplay
    {
        [Key]
        [Column("OrderId")]
        public int OrderId { get; set; }

        [Column("OrderStatus")]
        public string OrderStatus { get; set; } = "";

        [Column("OrderCreationDate")]
        public DateTime OrderCreationDate { get; set; }

        // В источнике DATE — можно оставить DateTime? либо перейти на DateOnly?
        [Column("OrderDeadline")]
        public DateTime? OrderDeadline { get; set; }

        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Column("ProjectTitle")]
        public string ProjectTitle { get; set; } = "";

        [Column("ProjectStatus")]
        public string ProjectStatus { get; set; } = "";

        [Column("CustomerId")]
        public int CustomerId { get; set; }

        [Column("CustomerFullName")]
        public string CustomerFullName { get; set; } = "";

        [Column("FreelancerId")]
        public int? FreelancerId { get; set; }

        [Column("FreelancerFullName")]
        public string FreelancerFullName { get; set; } = "";
    }
}
