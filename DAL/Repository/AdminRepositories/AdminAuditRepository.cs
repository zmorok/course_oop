using DAL.Context;
using DAL.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.AdminRepositories
{
    public interface IAdminAuditRepository
    {
        Task<List<AuditLog>> GetLogs(DateTime? since = null, DateTime? until = null, int? limit = 500);
        Task ExportLogs(string filename, DateTime? since = null, DateTime? until = null);
        Task ImportLogs(string filename);
    }

    public sealed class AdminAuditRepository : IAdminAuditRepository
    {
        private readonly FreelanceAppContext _context;
        public AdminAuditRepository(FreelanceAppContext context) => _context = context;

        public async Task<List<AuditLog>> GetLogs(DateTime? since = null, DateTime? until = null, int? limit = 500)
        {
            // core.admin_get_audit_logs(p_since TIMESTAMP, p_until TIMESTAMP)
            var logs = await _context
                .Set<AuditLog>()
                .FromSqlInterpolated($@"
                    SELECT *
                    FROM core.admin_get_audit_logs({since}::timestamp, {until}::timestamp)
                    ORDER BY changed_at DESC
                    LIMIT {limit ?? 500}")
                .AsNoTracking()
                .ToListAsync();

            return logs;
        }

        public Task ExportLogs(string filename, DateTime? since, DateTime? until) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_export_audit_logs_json({filename}, {since}, {until})");

        public Task ImportLogs(string filename) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_import_audit_logs_json({filename})");
    }
}
