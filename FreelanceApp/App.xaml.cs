using System.Windows;
using Microsoft.Extensions.Configuration;

namespace FreelanceApp
{
    public partial class App : Application
    {
        public static IConfiguration? Configuration { get; private set; }

        // начальное подключение (svc_app)
        public static string? DefaultConnection => Configuration.GetConnectionString("DefaultConnection");

        // текущее подключение, используемое в контексте
        public static string? ConnectionString { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // по умолчанию — подключение svc_app
            ConnectionString = DefaultConnection;
        }

        public static string GetConnectionForRole(string pgRole)
        {
            return Configuration[$"ConnectionStrings:{pgRole}"];
        }

        public static void ResetConnection()
        {
            ConnectionString = DefaultConnection;
        }
    }
}
