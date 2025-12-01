using System.IO;
using System.Windows;
using System.Windows.Input;
using FreelanceApp.Services;
using Npgsql;

namespace FreelanceApp.Authentication
{
    public partial class StartupWindow : Window
    {
        public StartupWindow()
        {
            InitializeComponent();
            //Loaded += (_, _) => LoadedChecker();
            DataContext = this;
        }

        // отладка подключения к бд
        private void LoadedChecker()
        {
            try
            {
                //EnsureDatabase(App.GetConnectionForRole("pg_test")).GetAwaiter().GetResult();

                using var conn = new NpgsqlConnection(App.GetConnectionForRole("svc_app"));
                conn.Open();
                conn.Close();
                MessageBox.Show("Успешно подключилось.");
            }
            catch (Exception ex)
            {
                var error = ex.InnerException ?? ex;
                MessageBox.Show(
                    $"Не удалось соединиться с БД:\n{error.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        public ICommand OpenLoginWindowCommand =>
            new RelayCommand(() =>
            {
                new LoginWindow().Show();
                Close();
            });

        public ICommand OpenRegisterWindowCommand =>
            new RelayCommand(() =>
            {
                new RegisterWindow().Show();
                Close();
            });

        private async Task EnsureDatabase(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var targetDb = builder.Database;
            builder.Database = "postgres";

            string script = File.OpenText("InitDatabase.sql").ReadToEnd();
            MessageBox.Show(script);

            using var adminConn = new NpgsqlConnection(builder.ConnectionString);
            using var initConn = new NpgsqlConnection(connectionString);

            NpgsqlCommand? createCmd = null;

            adminConn.Open();
            var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @db", adminConn);
            checkCmd.Parameters.AddWithValue("db", targetDb);

            var exists = checkCmd.ExecuteScalar() != null;
            if (!exists) {
                createCmd = new NpgsqlCommand($"CREATE DATABASE \"{targetDb}\"", adminConn);
                createCmd.ExecuteNonQuery();
            }
            initConn.Open();

            
            
            createCmd = new NpgsqlCommand(script, initConn);
            
            createCmd.ExecuteNonQuery();
            adminConn.Close();
            initConn.Close();
        }
    }
}
