using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Models.Views
{
    [Table("v_orders_reviews", Schema = "core")]
    public sealed class OrderReviewsRow
    {
        [Key]
        [Column("order_id")]
        public int Order_Id { get; set; }

        [Column("creation_date")]
        public DateTime Creation_Date { get; set; }

        [Column("project_title")]
        public string Project_Title { get; set; } = "";

        [Column("id_customer")]
        public int Id_Customer { get; set; }

        [Column("customer_fullname")]
        public string Customer_Fullname { get; set; } = "";

        [Column("customer_review_id")]
        public int? Customer_Review_Id { get; set; }

        [Column("customer_comment")]
        public string? Customer_Comment { get; set; }

        [Column("customer_rating")]
        public int? Customer_Rating { get; set; }

        [Column("id_freelancer")]
        public int? Id_Freelancer { get; set; }

        [Column("freelancer_fullname")]
        public string? Freelancer_Fullname { get; set; }

        [Column("freelancer_review_id")]
        public int? Freelancer_Review_Id { get; set; }

        [Column("freelancer_comment")]
        public string? Freelancer_Comment { get; set; }

        [Column("freelancer_rating")]
        public int? Freelancer_Rating { get; set; }
    }
}
