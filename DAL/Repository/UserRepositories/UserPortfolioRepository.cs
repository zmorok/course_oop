using DAL.Context;
using DAL.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repository.UserRepositories
{
    public interface IUserPortfolioRepository
    {
        Task<List<Portfolio>> GetPortfoliosAsync(int userId);

        Task CreatePortfolioAsync(
            int actorId,
            int userId,
            string description,
            string mediaJson,
            string[] skills,
            string experience
        );

        Task UpdatePortfolioAsync(
            int actorId,
            int userId,
            int portfolioId,
            string description,
            string mediaJson,
            string[] skills,
            string experience
        );

        Task DeletePortfolioAsync(int actorId, int userId, int portfolioId);
    }

    public class UserPortfolioRepository(FreelanceAppContext context) : IUserPortfolioRepository
    {
        private readonly FreelanceAppContext _context = context;

        public async Task<List<Portfolio>> GetPortfoliosAsync(int userId)
        {
            return await _context
                .Set<Portfolio>()
                .FromSqlInterpolated(
                    $@"
                    SELECT *
                      FROM core.user_get_portfolios({userId})
                "
                )
                .ToListAsync();
        }

        public Task CreatePortfolioAsync(
            int actorId,
            int userId,
            string description,
            string mediaJson,
            string[] skills,
            string experience
        )
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_create_portfolio(
                    {actorId},
                    {userId},
                    {description},
                    CAST({mediaJson} AS jsonb),
                    {skills},
                    {experience}
                )
            "
            );
        }

        public Task UpdatePortfolioAsync(
            int actorId,
            int userId,
            int portfolioId,
            string description,
            string mediaJson,
            string[] skills,
            string experience
        )
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_update_portfolio(
                    {actorId},
                    {userId},
                    {portfolioId},
                    {description},
                    CAST({mediaJson} AS jsonb),
                    {skills},
                    {experience}
                )
            "
            );
        }

        public Task DeletePortfolioAsync(int actorId, int userId, int portfolioId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync(
                $@"
                CALL core.user_delete_portfolio(
                    {actorId},
                    {userId},
                    {portfolioId}
                )
            "
            );
        }
    }
}
