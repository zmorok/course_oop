using System;
using DAL.Models.Tables;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;

namespace DAL.Context
{
    public class FreelanceAppContext : DbContext
    {
        private readonly string? _connectionString;

        // конструктор для DI
        public FreelanceAppContext(DbContextOptions<FreelanceAppContext> options) : base(options) { }

        // удобный конструктор по строке подключения (если без DI)
        public FreelanceAppContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        // --- Tables
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Complaint> Complaints { get; set; } = null!;
        public DbSet<Portfolio> Portfolios { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        // --- Views / function DTO
        public DbSet<UserNotification> UserNotifications { get; set; } = null!;
        public DbSet<UserWarning> WarningDisplays { get; set; } = null!;
        public DbSet<LocalOrderDisplay> LocalOrders { get; set; } = null!;
        public DbSet<OrderWithMyReview> LocalOrdersWithMyReview { get; set; } = null!;
        public DbSet<OrderReviewsRow> LocalOrdersReviews { get; set; } = null!;
        public DbSet<OrdersArchiveForComplaint> OrdersArchiveForComplaint { get; set; } = null!;
        public DbSet<Counterpart> LocalCounterparts { get; set; } = null!;
        public DbSet<MyComplaint> LocalMyComplaints { get; set; } = null!;
        public DbSet<AdminComplaint> AdminComplaints { get; set; } = null!;
        public DbSet<FreelancerRow> FreelancerRows { get; set; } = null!;
        public DbSet<ProjectWithoutStatus> ProjectWithoutStatuses { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured && _connectionString is not null)
                optionsBuilder.UseNpgsql(_connectionString);
        }
    }
}
