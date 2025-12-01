using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Views
{
    [Table("v_order_archive_extended", Schema = "core")]
    public class OrdersArchiveForComplaint
    {
        // --- Заказ (архив) ---
        [Key]
        [Column("OrderArcId")]
        public int OrderArcId { get; set; }

        [Column("OrderId")]
        public int OrderId { get; set; }

        [Column("OrderStatus")]
        public string? OrderStatus { get; set; }   // completed / cancelled (может быть NULL)

        [Column("OrderCreationDate")]
        public DateTime? OrderCreationDate { get; set; } // TIMESTAMP NULL

        [Column("OrderDeadline")]
        public DateTime? OrderDeadline { get; set; }     // DATE -> мапим как DateTime?

        // --- Проект ---
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        [Column("ProjectTitle")]
        public string ProjectTitle { get; set; } = string.Empty;

        [Column("ProjectStatus")]
        public string ProjectStatus { get; set; } = "archived"; // константа из вьюхи

        // --- Заказчик ---
        [Column("CustomerId")]
        public int CustomerId { get; set; }

        [Column("CustomerFullName")]
        public string CustomerFullName { get; set; } = string.Empty;

        // --- Исполнитель ---
        [Column("FreelancerId")]
        public int? FreelancerId { get; set; }  // в архиве может быть NULL

        [Column("FreelancerFullName")]
        public string FreelancerFullName { get; set; } = string.Empty;
    }
}
