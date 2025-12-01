using DAL.Context;
using DAL.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.AdminRepositories
{
    public interface IAdminUsersRepository
    {
        Task<List<User>> GetUsersAsync();
        Task<List<Role>> GetRolesAsync();

        Task CreateUserAsync(
            int actorId,
            string? passwordHash,
            int? roleId,
            string lastName,
            string firstName,
            string? middleName,
            string gender,
            string? phoneNumber,
            string email,
            decimal rating
        );

        Task UpdateUserAsync(
            int actorId,
            int userId,
            string? passwordHash,
            int? roleId,
            string lastName,
            string firstName,
            string? middleName,
            string gender,
            string? phoneNumber,
            string email,
            decimal rating
        );

        Task DeleteUserAsync(int actorId, int userId);
    }

    public sealed class AdminUsersRepository : IAdminUsersRepository
    {
        private readonly FreelanceAppContext _context;
        public AdminUsersRepository(FreelanceAppContext context) => _context = context;

        public Task<List<User>> GetUsersAsync() =>
            _context.Set<User>()
                    .FromSqlRaw("SELECT * FROM core.admin_get_users()")
                    .AsNoTracking()
                    .ToListAsync();

        public Task<List<Role>> GetRolesAsync() =>
            _context.Set<Role>()
                    .FromSqlRaw("SELECT * FROM core.admin_get_roles()")
                    .AsNoTracking()
                    .ToListAsync();

        public Task CreateUserAsync(
            int actorId,
            string? passwordHash,
            int? roleId,
            string lastName,
            string firstName,
            string? middleName,
            string gender,
            string? phoneNumber,
            string email,
            decimal rating
        ) =>
            _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.admin_create_user(
                    {actorId},
                    {passwordHash},
                    {roleId},
                    {lastName},
                    {firstName},
                    {middleName},
                    {gender},
                    {phoneNumber},
                    {email},
                    {rating}
                )");

        public Task UpdateUserAsync(
            int actorId,
            int userId,
            string? passwordHash,
            int? roleId,
            string lastName,
            string firstName,
            string? middleName,
            string gender,
            string? phoneNumber,
            string email,
            decimal rating
        ) =>
            _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.admin_update_user(
                    {actorId},
                    {userId},
                    {passwordHash},
                    {roleId},
                    {lastName},
                    {firstName},
                    {middleName},
                    {gender},
                    {phoneNumber},
                    {email},
                    {rating}
                )");

        public Task DeleteUserAsync(int actorId, int userId) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.admin_delete_user({actorId}, {userId})");
    }
}
