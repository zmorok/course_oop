using DAL.Context;
using DAL.Models.Tables;
using FreelanceApp.Services;
using FreelanceApp.Windows;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;

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
                string text = string.Empty, caption = string.Empty;

                if (string.IsNullOrEmpty(LoginBox.Text) || string.IsNullOrEmpty(PasswordBox.Password))
                {
                    text = Application.Current.TryFindResource("LoginWindow_Info_ErrorText") as string ?? "Пожалуйста, заполните все обязательные поля.";
                    caption = Application.Current.TryFindResource("LoginWindow_Info_ErrorText_Caption") as string ?? "Ошибка";

                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string email = LoginBox.Text;
                string password = PasswordBox.Password;
                string hash = HashPassword(password);

                using var ctx = new FreelanceAppContext(App.GetConnectionForRole("svc_app"));

                User? user = await ctx
                    .Users.Include(u => u.Role)
                    .SingleOrDefaultAsync(u => u.Email == email && u.Password == hash);

                if (user == null)
                {
                    text = Application.Current.TryFindResource("LoginWindow_Info_ErrorPassword") as string ?? "Неверный логин или пароль!";
                    caption = Application.Current.TryFindResource("LoginWindow_Info_ErrorPassword_Caption") as string ?? "Ошибка";

                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Window nextWindow = user.Role.Name switch
                {
                    "admin" => new AdminDashboardWindow(user),
                    _ => new UserDashboardWindow(user),
                };

                text = Application.Current.TryFindResource("LoginWindow_Info_Greetings") as string ?? "Добро пожаловать!";
                caption = Application.Current.TryFindResource("LoginWindow_Info_Greetings_Caption") as string ?? "Успех";

                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);

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
