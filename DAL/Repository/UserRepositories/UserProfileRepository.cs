using DAL.Context;
using DAL.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.UserRepositories
{
    public interface IUserProfileRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task UpdateProfileAsync
        (
            int actorId, int userId, string? newPasswordHash, string lastName,
            string firstName, string? middleName, string? gender, string? phoneNumber,
            string email, byte[]? photoBytes
        );
        Task<List<UserNotification>> GetNotificationsAsync(int userId);
        Task<List<UserWarning>> GetWarningsAsync(int userId);
        Task AcceptInviteAsync(int actorId, int notificationId, int userId);
        Task DeclineInviteAsync(int actorId, int notificationId, int userId);
    }

    public class UserProfileRepository : Repository<User>, IUserProfileRepository
    {
        public UserProfileRepository(FreelanceAppContext context) : base(context) { }

        public async Task<User?> GetByEmailAsync(string email) 
            => await _dbSet.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);
        
        public async Task UpdateProfileAsync
        (
            int actorId, int userId, string? newPasswordHash, string lastName,
            string firstName, string? middleName, string? gender, string? phoneNumber,
            string email, byte[]? photoBytes
        )
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_update_profile(
                    {actorId},
                    {userId},
                    {newPasswordHash},
                    {lastName},
                    {firstName},
                    {middleName},
                    {gender},
                    {phoneNumber},
                    {email},
                    {photoBytes}
                )");
        }

        public async Task<List<UserNotification>> GetNotificationsAsync(int userId)
        {
            return await _context.Set<UserNotification>()
                .FromSqlInterpolated($@"
                    SELECT * 
                    FROM core.v_user_notifications 
                    WHERE id_receiver = {userId} 
                    ORDER BY created_at DESC")
                .ToListAsync();
        }

        public async Task<List<UserWarning>> GetWarningsAsync(int userId)
        {
            return await _context.Set<UserWarning>()
                .FromSqlInterpolated($@"
                    SELECT id_warning, admin_name, message, expires_at 
                    FROM core.v_user_warnings 
                    WHERE user_id = {userId} 
                    ORDER BY expires_at")
                .ToListAsync();
        }

        public Task AcceptInviteAsync(int actorId, int notificationId, int userId)
            => _context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL core.user_accept_invite({actorId}, {notificationId}, {userId})");

        public Task DeclineInviteAsync(int actorId, int notificationId, int userId)
            => _context.Database.ExecuteSqlInterpolatedAsync(
                $"CALL core.user_decline_invite({actorId}, {notificationId}, {userId})");
    }
}
