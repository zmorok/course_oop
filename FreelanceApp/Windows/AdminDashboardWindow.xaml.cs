using DAL.Models.Tables;
using FreelanceApp.Authentication;
using FreelanceApp.Services;
using FreelanceApp.Windows.AdminControls;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace FreelanceApp.Windows
{
    public partial class AdminDashboardWindow : Window
    {
        private readonly User _currentUser;

        public AdminDashboardWindow(User currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;

            // вызывать асинхронную навигацию после загрузки окна
            Loaded += async (_, __) =>
            {
                UpdateThemeButtons();
                await ShowUsers();
            };

            Closing += async (_, __) => await UpdateLastOnlineAsync();
        }

        private async Task UpdateLastOnlineAsync()
        {
            try
            {
                await using var context = DbContextFactory.CreateDbContext(_currentUser);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"CALL core.update_user_last_online({_currentUser.Id}, {_currentUser.Id})"
                );
            }
            catch (Exception ex)
            {
                ex = ex.InnerException ?? ex;
                MessageBox.Show("Ошибка обновления статуса онлайн:\n\n" + ex.Message,
                    "Ошибка обновления статуса онлайн", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private async void ShowUsers_Click(object sender, RoutedEventArgs e) => await ShowUsers();
        private async Task ShowUsers()
        {
            var vm = new UserManagementControl();
            await vm.InitializeAsync(_currentUser);
            MainContent.Content = vm;
        }

        private async void ShowRoles_Click(object sender, RoutedEventArgs e)
        {
            var vm = new RolesManagementControl();
            await vm.InitializeAsync(_currentUser);
            MainContent.Content = vm;
        }

        private void ShowAudit_Click(object sender, RoutedEventArgs e) =>
            MainContent.Content = new AuditLogsControl(_currentUser);

        private async void ShowComplaints_Click(object sender, RoutedEventArgs e)
        {
            var vm = new ComplaintsModerationControl();
            await vm.InitializeAsync(_currentUser);
            MainContent.Content = vm;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            App.ResetConnection();
            new StartupWindow().Show();
            Close();
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(AppTheme.Light);

        private void DarkThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(AppTheme.Dark);

        private void ApplyTheme(AppTheme theme)
        {
            ThemeManager.Apply(theme);
            UpdateThemeButtons();
        }

        private void UpdateThemeButtons()
        {
            if (AdminLightThemeButton == null || AdminDarkThemeButton == null) return;

            AdminLightThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Light;
            AdminDarkThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Dark;
        }
    }
}
