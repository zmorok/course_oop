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
                // дата рожления
                //DateTime? birthDate = BirthDatePicker.SelectedDate;
                //if (birthDate == null)
                //{
                //    MessageBox.Show( "Пожалуйста, укажите дату рождения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    return;
                //}

                //DateTime today = DateTime.Today;
                //int age = today.Year - birthDate.Value.Year;
                //if (birthDate.Value.Date > today.AddYears(-age))
                //    age--;

                //if (age < 18)
                //{
                //    MessageBox.Show(
                //        "Регистрация разрешена только с 18 лет и старше.",
                //        "Ошибка",
                //        MessageBoxButton.OK,
                //        MessageBoxImage.Warning);
                //    return;
                //}

                string firstName = FirstNameBox.Text.Trim();
                string lastName = LastNameBox.Text.Trim();
                string email = EmailBox.Text.Trim();
                string password = PasswordBox.Password;
                string phone = PhoneBox.Text.Trim();
                string? gender = ((ComboBoxItem)GenderBox.SelectedItem)?.Content?.ToString();

                if (
                    string.IsNullOrWhiteSpace(email)
                    || string.IsNullOrWhiteSpace(password)
                    || string.IsNullOrWhiteSpace(firstName)
                    || string.IsNullOrWhiteSpace(lastName)
                )
                {
                    MessageBox.Show(
                        "Пожалуйста, заполните все обязательные поля.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }

                string hash = HashPassword(password);
                using var context = new FreelanceAppContext(App.GetConnectionForRole("svc_app"));

                if (await context.Users.AnyAsync(u => u.Email == email))
                {
                    MessageBox.Show(
                        "Email уже используется",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                Role? clientRole = await context.Roles.SingleOrDefaultAsync(r => r.Name == "user");
                if (clientRole == null)
                {
                    MessageBox.Show(
                        "Роль 'user' не найдена в базе данных.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                User user = new()
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    Password = hash,
                    PhoneNumber = phone,
                    Gender = gender!,
                    RoleId = clientRole.Id,
                    RegistrationDate = DateTime.UtcNow,
                    Rating = 0.0m,
                };

                context.Users.Add(user);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    ex = ex.InnerException ?? ex;
                    MessageBox.Show(
                        $"Ошибка при регистрации: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }

                MessageBox.Show(
                    $"Вы успешно зарегистрированы!\n"
                        + "Ваши данные для входа:\n\n"
                        + $"-Логин:   {user.Email}\n"
                        + $"-Пароль:  {password}",
                    "Регистрация завершена",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

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
