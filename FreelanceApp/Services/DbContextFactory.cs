using DAL.Models.Tables;

namespace FreelanceApp.Services
{
    public static class DbContextFactory
    {
        public static DAL.Context.FreelanceAppContext CreateDbContext(User user)
        {
            string role = user.Role.Name switch
            {
                "admin" => "svc_admin",
                _ => "app_end_usr",
            };

            string currentConnectionString = App.GetConnectionForRole(role);

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
