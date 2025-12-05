using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DAL.Models.Tables;
using DAL.Context;
using FreelanceApp.Services;
using FreelanceApp.Windows;
using Microsoft.EntityFrameworkCore;

namespace FreelanceApp.Authentication
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ICommand LoginCommand =>
            new RelayCommand(async () =>
            {
                string email = UsernameBox.Text;
                string password = PasswordBox.Password;
                string hash = HashPassword(password);

                MessageBox.Show($"email: {email}\npassword: {password}\nhash: {hash}");

                using var ctx = new FreelanceAppContext(App.GetConnectionForRole("svc_app"));

                User? user = await ctx
                    .Users.Include(u => u.Role)
                    .SingleOrDefaultAsync(u => u.Email == email && u.Password == hash);

                if (user == null)
                {
                    MessageBox.Show(
                        "Неверные данные",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                MessageBox.Show($"email: {user.Email}\npassword: {user.Password}\n");

                Window nextWindow = user.Role.Name switch
                {
                    "admin" => new AdminDashboardWindow(user),
                    _ => new UserDashboardWindow(user),
                };

                nextWindow.Show();
                Close();
            });

        public ICommand GoBackCommand =>
            new RelayCommand(() =>
            {
                new StartupWindow().Show();
                Close();
            });

        private static string HashPassword(string plain)
        {
            byte[] bytes = SHA512.HashData(Encoding.UTF8.GetBytes(plain));
            return Convert.ToHexString(bytes);
        }
    }
}
