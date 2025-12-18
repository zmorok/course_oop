using DAL.Models.Tables;

namespace FreelanceApp.Services
{
    public static class DbContextFactory
    {
        public static DAL.Context.FreelanceAppContext CreateDbContext(User user)
        {
            // Если по какой-то причине пользователя нет — подставляем заглушку,
            // чтобы не получить NullReference и было проще отладить место вызова.
            user ??= new User
            {
                Role = new Role { Name = "app_end_usr" },
                FirstName = "Stub",
                LastName = "User"
            };

            string role = user.Role?.Name switch
            {
                "admin" => "svc_admin",
                _ => "app_end_usr",
            };

            string currentConnectionString = App.GetConnectionForRole(role);
            if (string.IsNullOrWhiteSpace(currentConnectionString))
            {
                currentConnectionString = App.ConnectionString ?? App.DefaultConnection;
            }

            if (string.IsNullOrWhiteSpace(currentConnectionString))
            {
                throw new InvalidOperationException(
                    "App.ConnectionString is null or empty. Ensure it is set correctly (usually after login)."
                );
            }

            return new DAL.Context.FreelanceAppContext(currentConnectionString);
        }
    }
}
