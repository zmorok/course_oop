using DAL.Context;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DAL.Repository.UserRepositories
{
    public interface IUserReviewsRepository
    {
        Task<List<OrderReviewsRow>> GetOrderReviewsAsync(int userId, bool asCustomer);
        Task CreateReviewAsync(
            int actorId,
            int orderId,
            int reviewerId,
            string comment,
            int rating
        );
        Task UpdateReviewAsync(
            int actorId,
            int reviewId,
            int reviewerId,
            string comment,
            int rating
        );
        Task DeleteReviewAsync(int actorId, int reviewId, int reviewerId);
    }

    public class UserReviewsRepository(FreelanceAppContext context) : IUserReviewsRepository
    {
        private readonly FreelanceAppContext _context = context;

        public async Task<List<OrderReviewsRow>> GetOrderReviewsAsync(int userId, bool asCustomer)
        {
            var column = asCustomer ? "id_customer" : "id_freelancer";
            var sql =
                $@"
                    SELECT *
                      FROM core.v_orders_reviews
                     WHERE {column} = @uid
                     ORDER BY creation_date DESC";

            var uidParam = new NpgsqlParameter("@uid", userId);

            return await _context
                .Set<OrderReviewsRow>()
                .FromSqlRaw(sql, uidParam)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task CreateReviewAsync(
            int actorId,
            int orderId,
            int reviewerId,
            string comment,
            int rating
        ) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.user_create_review(
                        {actorId},
                        {orderId},
                        {reviewerId},
                        {comment},
                        {rating},
                        CAST(NULL AS JSONB)
                    )"
            );

        public Task UpdateReviewAsync(
            int actorId,
            int reviewId,
            int reviewerId,
            string comment,
            int rating
        ) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.user_update_review(
                    {actorId},
                    {reviewId},
                    {reviewerId},
                    {comment},
                    {rating},
                    NULL
                )"
            );

        public Task DeleteReviewAsync(int actorId, int reviewId, int reviewerId) =>
            _context.Database.ExecuteSqlInterpolatedAsync(
                $@"CALL core.user_delete_review(
                    {actorId},
                    {reviewId},
                    {reviewerId}
                )"
            );
    }
}