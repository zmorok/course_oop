using DAL.Context;
using DAL.Models.Tables;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DAL.Repository.AdminRepositories
{
    public interface IAdminRolesRepository
    {
        Task<List<AdminRole>> GetRolesAsync();
        Task CreateRole(int actorId, string text, JsonElement json);
        Task UpdateRole(int actorId, int roleId, JsonElement json);
        Task DeleteRole(int actorId, int roleId);
    }

    public sealed class AdminRolesRepository : IAdminRolesRepository
    {
        private readonly FreelanceAppContext _context;
        public AdminRolesRepository(FreelanceAppContext context) => _context = context;

        public async Task<List<AdminRole>> GetRolesAsync()
        {
            // SETOF core.roles
            var roles = await _context.Set<Role>()
                                      .FromSqlRaw("SELECT * FROM core.admin_get_roles()")
                                      .AsNoTracking()
                                      .ToListAsync();

            return roles.Select(r => new AdminRole
            {
                Id = r.Id,
                Name = r.Name,
                Privileges = r.Privileges?.RootElement.GetRawText() ?? "{}"
            }).ToList();
        }

        public Task CreateRole(int actorId, string text, JsonElement json) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_create_role({actorId}, {text}, {json})");

        public Task UpdateRole(int actorId, int roleId, JsonElement json) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_update_role({actorId}, {roleId}, {json})");

        public Task DeleteRole(int actorId, int roleId) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_delete_role({actorId}, {roleId})");
    }
}
