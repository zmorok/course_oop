// DAL/Repository/AdminRepositories/AdminModerationRepository.cs
using DAL.Context;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.AdminRepositories
{
    public interface IAdminModerationRepository
    {
        Task<List<AdminComplaint>> GetComplaintsAsync(string? mode = null); // all|unsolved|resolved|null
        Task UpdateComplaintStatusAsync(int actorId, int complaintId, string newStatus, int adminId);
        Task ResolveComplaintAsync(int actorId, int complaintId, string resolutionStatus, int adminId);
        Task IssueWarningAsync(int actorId, int complaintId, int targetUserId, string message, int expiresDays = 7);
        Task<string> CheckUserAsync(int actorId, int targetUserId);
    }

    public sealed class AdminModerationRepository : IAdminModerationRepository
    {
        private readonly FreelanceAppContext _context;
        public AdminModerationRepository(FreelanceAppContext context) => _context = context;

        public async Task<List<AdminComplaint>> GetComplaintsAsync(string? mode = null)
        {
            // Берём из вьюхи v_mod_complaints (богатая проекция для UI)
            var baseQuery = _context.Set<AdminComplaint>()
                                    .FromSqlRaw(@"SELECT * FROM core.v_admin_complaints")
                                    .AsNoTracking();

            if (string.IsNullOrWhiteSpace(mode) || mode == "all")
                return await baseQuery.ToListAsync();

            return await baseQuery.Where(c =>
                        mode == "unsolved"
                          ? (c.Status == "new" || c.Status == "in_progress")
                          : mode == "resolved" && c.Status == "resolved")
                    .ToListAsync();
        }

        public Task UpdateComplaintStatusAsync(int actorId, int complaintId, string newStatus, int adminId)
            => _context.Database.ExecuteSqlInterpolatedAsync($@"
                    CALL core.admin_update_complaint_status(
                        {actorId},
                        {complaintId},
                        {newStatus},
                        {adminId}
                    )");

        public Task ResolveComplaintAsync(int actorId, int complaintId, string resolutionStatus, int adminId)
            => _context.Database.ExecuteSqlInterpolatedAsync($@"
                    CALL core.admin_resolve_complaint(
                        {actorId},
                        {complaintId},
                        {resolutionStatus},
                        {adminId}
                    )");

        public Task IssueWarningAsync(int actorId, int complaintId, int targetUserId, string message, int expiresDays = 7)
            => _context.Database.ExecuteSqlInterpolatedAsync($@"
                    CALL core.admin_issue_warning(
                        {actorId},
                        {complaintId},
                        {targetUserId},
                        {message},
                        {expiresDays}
                    )");

        public async Task<string> CheckUserAsync(int actorId, int targetUserId)
        {
            // Возьмём scalar JSONB::text
            var json = await _context.Database
                .SqlQueryRaw<string>($@"SELECT core.admin_check_user({actorId}, {targetUserId})::text")
                .FirstAsync();

            return json ?? "{}";
        }
    }
}
