using DAL.Context;
using DAL.Models.Views;
using Microsoft.EntityFrameworkCore;


namespace DAL.Repository.UserRepositories
{
    public interface IUserOrdersRepository
    {
        Task<List<LocalOrderDisplay>> GetOrdersByCustomerAsync(int userId, int limit = 100);
        Task<List<LocalOrderDisplay>> GetOrdersByFreelancerAsync(int userId, int limit = 100);
        Task<List<LocalOrderDisplay>> GetArchiveOrdersAsync(int userId, int limit = 100);
        Task<List<OrdersArchiveForComplaint>> GetArchiveOrdersCounterpartsAsync(int userId, int limit = 100);
        Task CreateOrderAsync(int actorId, int projectId, int freelancerId, string status, DateTime? deadline);
        Task UpdateOrderAsync(int actorId, int orderId, string status, DateTime? deadline);
        Task DeleteOrderAsync(int actorId, int orderId);
    }

    public class UserOrdersRepository(FreelanceAppContext context) : IUserOrdersRepository
    {
        private readonly FreelanceAppContext _context = context;

        public async Task<List<LocalOrderDisplay>> GetOrdersByCustomerAsync(int userId, int limit = 100)
        {
            return await _context.Set<LocalOrderDisplay>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.v_order_extended
                     WHERE ""CustomerId"" = {userId}
                  ORDER BY ""OrderId"" DESC LIMIT {limit}")
                .ToListAsync();
        }

        public async Task<List<LocalOrderDisplay>> GetOrdersByFreelancerAsync(int userId, int limit = 100)
        {
            return await _context.Set<LocalOrderDisplay>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.v_order_extended
                     WHERE ""FreelancerId"" = {userId}
                  ORDER BY ""OrderId"" DESC LIMIT {limit}")
                .ToListAsync();
        }

        public async Task<List<LocalOrderDisplay>> GetArchiveOrdersAsync(int userId, int limit = 100)
        {
            return await _context.Set<LocalOrderDisplay>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.v_order_archive_extended
                     WHERE ""CustomerId"" = {userId} OR ""FreelancerId"" = {userId}
                  ORDER BY ""OrderId"" DESC LIMIT {limit}")
                .ToListAsync();
        }

        public async Task<List<OrdersArchiveForComplaint>> GetArchiveOrdersCounterpartsAsync(int userId, int limit = 100)
        {
            return await _context.Set<OrdersArchiveForComplaint>()
                .FromSqlInterpolated($@"
                    SELECT *
                      FROM core.v_order_archive_extended
                     WHERE ""CustomerId"" = {userId} OR ""FreelancerId"" = {userId}
                  ORDER BY ""OrderId"" DESC LIMIT {limit}")
                .ToListAsync();
        }

        public Task CreateOrderAsync(int actorId, int projectId, int freelancerId, string status, DateTime? deadline) =>
            _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_create_order(
                    {actorId}, {projectId}, {freelancerId}, {status}, {deadline}
                )");


        public Task UpdateOrderAsync(int actorId, int orderId, string status, DateTime? deadline)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_update_order(
                    {actorId},
                    {orderId},
                    {status},
                    CAST({deadline} as DATE)
                )");
        }

        public Task DeleteOrderAsync(int actorId, int orderId)
        {
            return _context.Database.ExecuteSqlInterpolatedAsync($@"
                CALL core.user_delete_order(
                    {actorId},
                    {orderId}
                )");
        }
    }
}