using DAL.Context;
using DAL.Models.Tables;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.UserRepositories
{
    public interface IUserProjectsRepository
    {
        Task<List<Project>> GetAllProjectsAsync(int limit = 100);
        Task<List<Project>> GetProjectsByCustomerAsync(
            int customerId,
            string? statusFilter = null,
            int limit = 100
        );
        Task<List<ProjectWithoutStatus>> GetProjectsWithoutStatusAsync(
            string statusFilter,
            int limit = 100
        );
        Task<List<Project>> GetProjectsByCustomerAndStatusAsync(
            int customerId,
            string? statusFilter = null,
            int limit = 100
        );
        Task RespondToProjectAsync(int actorId, int projectId);
        Task CreateProjectAsync(
            int actorId,
            int userId,
            string title,
            string status,
            string description,
            string mediaJson
        );
        Task UpdateProjectAsync(
            int actorId,
            int projectId,
            string title,
            string status,
            string description,
            string mediaJson
        );
        Task DeleteProjectAsync(int actorId, int projectId);
    }

    public class UserProjectsRepository(FreelanceAppContext context) : IUserProjectsRepository
    {
        private readonly FreelanceAppContext _context = context;

        public async Task<List<Project>> GetAllProjectsAsync(int limit = 100)
        {
            return await _context
                .Set<Project>()
                .FromSqlInterpolated($"SELECT * FROM core.user_get_projects() LIMIT {limit}")
                .ToListAsync();
        }

        public async Task<List<Project>> GetProjectsByCustomerAsync(
            int customerId,
            string? statusFilter = null,
            int limit = 100
        )
        {
            if (string.IsNullOrEmpty(statusFilter))
            {
                return await _context
                    .Set<Project>()
                    .FromSqlInterpolated(
                        $"SELECT * FROM core.user_get_projects_by_customer({customerId}) LIMIT {limit}"
                    )
                    .ToListAsync();
            }
            else
            {
                return await _context
                    .Set<Project>()
                    .FromSqlInterpolated(
                        $@"
                        SELECT *
                          FROM core.user_get_projects_by_customer({customerId})
                         WHERE status = {statusFilter}
                         LIMIT {limit}"
                    )
                    .ToListAsync();
            }
        }

        public async Task<List<ProjectWithoutStatus>> GetProjectsWithoutStatusAsync(
            string statusFilter,
            int limit = 100
        )
        {
            return await _context
                .Set<ProjectWithoutStatus>()
                .FromSqlInterpolated(
                    $@"
                    SELECT id_project, id_customer, title, description, media
                      FROM core.v_projects p
                     WHERE p.status = {statusFilter}
                  ORDER BY id_project
                     LIMIT {limit}"
                )
                .ToListAsync();
        }

        public Task RespondToProjectAsync(int actorId, int projectId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_create_order(
                    {actorId},
                    {projectId},
                    {actorId},
                    'pending',
                    NULL
                )"
            );
        }

        public Task CreateProjectAsync(
            int actorId,
            int userId,
            string title,
            string status,
            string description,
            string mediaJson
        )
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_create_project(
                    {actorId},
                    {userId},
                    {title},
                    {status},
                    {description},
                    CAST({mediaJson} AS jsonb)
                )"
            );
        }

        public Task UpdateProjectAsync(
            int actorId,
            int projectId,
            string title,
            string status,
            string description,
            string mediaJson
        )
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_update_project(
                    {actorId},
                    {projectId},
                    {title},
                    {status},
                    {description},
                    CAST({mediaJson} AS jsonb)
                )"
            );
        }

        public Task DeleteProjectAsync(int actorId, int projectId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_delete_project(
                    {actorId},
                    {projectId}
                )"
            );
        }

        public async Task<List<Project>> GetProjectsByCustomerAndStatusAsync(
            int customerId,
            string? statusFilter = null,
            int limit = 100
        )
        {
            // если статус не указан, просто возвращаем все проекты этого клиента
            if (string.IsNullOrEmpty(statusFilter))
                return await GetProjectsByCustomerAsync(customerId, null, limit);

            // иначе выбираем и фильтруем по переданному статусу
            return await _context
                .Set<Project>()
                .FromSqlInterpolated(
                    $@"
            SELECT *
              FROM core.user_get_projects_by_customer({customerId})
             WHERE status = {statusFilter}
             LIMIT {limit}"
                )
                .ToListAsync();
        }
    }
}
