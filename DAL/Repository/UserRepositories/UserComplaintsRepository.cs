using DAL.Context;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.UserRepositories
{
    public interface IUserComplaintsRepository
    {
        Task<List<Counterpart>> GetCounterpartsAsync(int userId);
        Task<List<MyComplaint>> GetComplaintsAsync(int userId);

        Task CreateComplaintAsync(
            int actorId,
            int filedById,
            int targetUserId,
            string description,
            string? mediaJson = null,
            int? orderId = null,
            int? orderArchiveId = null);

        Task UpdateComplaintAsync(
            int actorId,
            int complaintId,
            string description,
            string? mediaJson = null);

        Task DeleteComplaintAsync(int actorId, int complaintId);
    }

    public class UserComplaintsRepository(FreelanceAppContext context) : IUserComplaintsRepository
    {
        private readonly FreelanceAppContext _context = context;

        public Task<List<Counterpart>> GetCounterpartsAsync(int userId)
        {
            return _context.Set<Counterpart>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.user_counterparts({userId})
                ")
                .ToListAsync();
        }

        public Task<List<MyComplaint>> GetComplaintsAsync(int userId)
        {
            return _context.Set<MyComplaint>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.user_get_complaints({userId})
                ")
                .ToListAsync();
        }

        public Task CreateComplaintAsync(
            int actorId,
            int filedById,
            int targetUserId,
            string description,
            string? mediaJson = null,
            int? orderId = null,
            int? orderArchiveId = null)
        {
            // как в проектах/портфолио: всегда передаём json-массив, даже если он пустой ("[]")
            var json = string.IsNullOrWhiteSpace(mediaJson) ? "[]" : mediaJson;

            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_create_complaint(
                    {actorId},
                    {filedById},
                    {targetUserId},
                    {description},
                    CAST({json} AS jsonb),
                    {orderId},
                    {orderArchiveId}
                )");
        }

        public Task UpdateComplaintAsync(int actorId, int complaintId, string description, string? mediaJson = null)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_update_complaint(
                    {actorId},
                    {complaintId},
                    {description},
                    CAST({mediaJson} AS jsonb)
                )
            ");
        }

        public Task DeleteComplaintAsync(int actorId, int complaintId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_delete_complaint(
                    {actorId},
                    {complaintId}
                )
            ");
        }
    }
}
