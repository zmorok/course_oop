using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DAL.Models.Tables;
using DAL.Context;
using FreelanceApp.Services;
using Microsoft.EntityFrameworkCore;

namespace FreelanceApp.Authentication
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ICommand RegisterCommand =>
            new RelayCommand(async () =>
            {
                string firstName = FirstNameBox.Text.Trim();
                string lastName = LastNameBox.Text.Trim();
                string email = EmailBox.Text.Trim();
                string password = PasswordBox.Password;
                string phone = PhoneBox.Text.Trim();
                string? gender = ((ComboBoxItem)GenderBox.SelectedItem)?.Content?.ToString();

                string text = string.Empty, caption = string.Empty;

                if (
                    string.IsNullOrWhiteSpace(email)
                    || string.IsNullOrWhiteSpace(password)
                    || string.IsNullOrWhiteSpace(firstName)
                    || string.IsNullOrWhiteSpace(lastName)
                )
                {
                    text = Application.Current.TryFindResource("RegisterWindow_Info_ErrorText") as string ?? "Пожалуйста, заполните все обязательные поля.";
                    caption = Application.Current.TryFindResource("RegisterWindow_Info_ErrorText_Caption") as string ?? "Ошибка";

                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string hash = HashPassword(password);
                using var context = new FreelanceAppContext(App.GetConnectionForRole("svc_app"));

                if (await context.Users.AnyAsync(u => u.Email == email))
                {
                    text = Application.Current.TryFindResource("RegisterWindow_Info_EmailExist") as string ?? "Email уже используется.";
                    caption = Application.Current.TryFindResource("RegisterWindow_Info_EmailExist_Caption") as string ?? "Ошибка";

                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Role? clientRole = await context.Roles.SingleOrDefaultAsync(r => r.Name == "user");

                User user = new()
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    Password = hash,
                    PhoneNumber = phone,
                    Gender = gender!,
                    RoleId = clientRole?.Id ?? 2,
                    RegistrationDate = DateTime.UtcNow,
                    Rating = 0.0m,
                };

                context.Users.Add(user);
                try { await context.SaveChangesAsync(); }
                catch (Exception ex)
                {
                    text = Application.Current.TryFindResource("RegisterWindow_Info_RegError") as string ?? "Ошибка при регистрации: ";
                    caption = Application.Current.TryFindResource("RegisterWindow_Info_RegError_Caption") as string ?? "Ошибка";

                    ex = ex.InnerException ?? ex;
                    MessageBox.Show($"{text}{ex.Message}", caption, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var textSuccess = Application.Current.TryFindResource("RegisterWindow_Info_Success") as string ?? "Вы успешно зарегистрированы!\nВаши данные для входа:\n\n";
                var textSuccessEmail = Application.Current.TryFindResource("RegisterWindow_Info_SuccessEmail") as string ?? "-Логин:   ";
                var textSuccessPassword = Application.Current.TryFindResource("RegisterWindow_Info_SuccessPassword") as string ?? "-Пароль:   ";

                text = textSuccess + $"{textSuccessEmail}{user.Email}\n" + $"{textSuccessPassword}{password}";
                caption = Application.Current.TryFindResource("RegisterWindow_Info_Success_Caption") as string ?? "Регистрация завершена!";

                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);

                new StartupWindow().Show();
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
