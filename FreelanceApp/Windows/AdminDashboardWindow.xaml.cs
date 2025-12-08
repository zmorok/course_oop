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

            Loaded += async (_, __) =>
            {
                await ShowUsers();
                UpdateThemeButtons(); UpdateLocaleButtons(); SetTitle();
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
                var text = Application.Current.TryFindResource("_Info_OnlineStatus") as string ?? "Ошибка обновления статуса онлайн";

                ex = ex.InnerException ?? ex;
                MessageBox.Show($"{text}\n\n{ex.Message}\n\n", text, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private async void ShowUsers_Click(object sender, RoutedEventArgs e) => await ShowUsers();
        private async Task ShowUsers()
        {
            var vm = new UserManagementControl();
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

        private void SetRu_Click(object sender, RoutedEventArgs e) => ApplyLang(AppLanguage.Ru);

        private void SetEn_Click(object sender, RoutedEventArgs e) => ApplyLang(AppLanguage.En);

        private void ApplyLang(AppLanguage lang) { LocalizationManager.SetLanguage(lang); UpdateLocaleButtons(); SetTitle(); }

        private void UpdateLocaleButtons()
        {
            if (RuLangButton == null || EnLangButton == null) return;

            RuLangButton.IsEnabled = LocalizationManager.CurrentLanguage != AppLanguage.Ru;
            EnLangButton.IsEnabled = LocalizationManager.CurrentLanguage != AppLanguage.En;
        }

        private void SetTitle()
        {
            var baseTitle = Application.Current.TryFindResource("UserDashboard_Title") as string ?? "Панель пользователя";
            Title = $"{baseTitle}: {_currentUser.Email}";
        }
    }
}
