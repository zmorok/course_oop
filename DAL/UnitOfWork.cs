using Microsoft.EntityFrameworkCore.Storage;
using DAL.Context;
using DAL.Repository.UserRepositories;
using DAL.Repository.AdminRepositories;
using System;
using System.Threading.Tasks;

namespace DAL
{
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        // User area
        IUserProfileRepository Users { get; }
        IUserPortfolioRepository Portfolios { get; }
        IUserOrdersRepository Orders { get; }
        IUserProjectsRepository Projects { get; }
        IUserReviewsRepository Reviews { get; }
        IUserComplaintsRepository Complaints { get; }
        IUserSearchRepository Search { get; }

        // Admin area
        IAdminUsersRepository AdminUsers { get; }
        IAdminRolesRepository AdminRoles { get; }
        IAdminAuditRepository AdminAudit { get; }
        IAdminModerationRepository AdminModeration { get; }   // NEW

        Task<int> CompleteAsync();
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
    }

    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly FreelanceAppContext _context;
        private IDbContextTransaction? _transaction;

        public IUserProfileRepository Users { get; }
        public IUserPortfolioRepository Portfolios { get; }
        public IUserOrdersRepository Orders { get; }
        public IUserProjectsRepository Projects { get; }
        public IUserReviewsRepository Reviews { get; }
        public IUserComplaintsRepository Complaints { get; }
        public IUserSearchRepository Search { get; }

        public IAdminUsersRepository AdminUsers { get; }
        public IAdminRolesRepository AdminRoles { get; }
        public IAdminAuditRepository AdminAudit { get; }
        public IAdminModerationRepository AdminModeration { get; } // NEW

        public UnitOfWork(FreelanceAppContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // user repos
            Users = new UserProfileRepository(_context);
            Portfolios = new UserPortfolioRepository(_context);
            Orders = new UserOrdersRepository(_context);
            Projects = new UserProjectsRepository(_context);
            Reviews = new UserReviewsRepository(_context);
            Complaints = new UserComplaintsRepository(_context);
            Search = new UserSearchRepository(_context);

            // admin repos
            AdminUsers = new AdminUsersRepository(_context);
            AdminRoles = new AdminRolesRepository(_context);
            AdminAudit = new AdminAuditRepository(_context);
            AdminModeration = new AdminModerationRepository(_context); // NEW
        }

        public async Task BeginTransactionAsync()
        {
            if (_transaction is null)
                _transaction = await _context.Database.BeginTransactionAsync().ConfigureAwait(false);
        }

        public Task<int> CompleteAsync() => _context.SaveChangesAsync();

        public async Task CommitAsync()
        {
            if (_transaction is not null)
            {
                await _transaction.CommitAsync().ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync().ConfigureAwait(false);
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction is not null)
            {
                await _transaction.DisposeAsync().ConfigureAwait(false);
                _transaction = null;
            }
            await _context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
