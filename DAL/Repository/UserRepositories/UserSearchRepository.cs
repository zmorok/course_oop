using DAL.Context;
using DAL.Models.Tables;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;


namespace DAL.Repository.UserRepositories
{
    public interface IUserSearchRepository
    {
        Task<List<FreelancerRow>> SearchFreelancersAsync(int currentUserId, string query);
        Task<List<Project>> GetFreeProjectsAsync(int currentUserId, int freelancerId, int limit = 20);
        Task SendProjectInviteAsync(int actorId, int inviteeId, int projectId);
    }

    public class UserSearchRepository(FreelanceAppContext context) : IUserSearchRepository
    {
        private readonly FreelanceAppContext _context = context;

        public Task<List<FreelancerRow>> SearchFreelancersAsync(int currentUserId, string query)
        {
            return _context.Set<FreelancerRow>()
                .FromSqlInterpolated($@"
                    SELECT * 
                      FROM core.search_users(
                        current_user_id := {currentUserId},
                        query            := {query}
                      )")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<Project>> GetFreeProjectsAsync(int currentUserId, int freelancerId, int limit = 20)
        {
            return _context.Set<Project>()
                .FromSqlInterpolated($@"
                    SELECT * 
                      FROM core.free_projects(
                        current_user_id := {currentUserId},
                        freelancer_id    := {freelancerId}
                      )
                     LIMIT {limit}")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task SendProjectInviteAsync(int actorId, int inviteeId, int projectId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_send_project_invite(
                    {actorId},
                    {actorId},
                    {inviteeId},
                    {projectId}
                )");
        }
    }
}
