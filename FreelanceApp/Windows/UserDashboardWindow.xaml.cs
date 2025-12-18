using DAL.Models.Tables;
using FreelanceApp.Authentication;
using FreelanceApp.Services;
using FreelanceApp.Windows.UserControls;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace FreelanceApp.Windows
{
    public partial class UserDashboardWindow : Window
    {
        private readonly User _currentUser;

        public UserDashboardWindow(User user)
        {
            _currentUser = user;

            Loaded += (_, __) =>
            {
                if (TabControlMain.SelectedItem is TabItem t) InitTab(t);
                UpdateThemeButtons(); UpdateLocaleButtons(); SetTitle();
            };
            Closing += async (_, _) => await UpdateLastOnlineAsync();
            InitializeComponent();
        }

        private readonly HashSet<string> _initializedTabs = [];

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem t) InitTab(t);
        }

        private async void InitTab(TabItem tab)
        {
            if (tab.Tag is not string key || !_initializedTabs.Add(key)) return;

            switch (key)
            {
                case "Profile":
                    await ProfileControl.InitializeAsync(_currentUser);
                    break;
                case "Portfolio":
                    await PortfolioControl.InitializeAsync(_currentUser);
                    break;
                case "Orders":
                    await OrdersControl.InitializeAsync(_currentUser);
                    break;
                case "Projects":
                    await ProjectsControl.InitializeAsync(_currentUser);
                    break;
                case "Reviews":
                    await ReviewsControl.InitializeAsync(_currentUser);
                    break;
                case "Complaints":
                    await ComplaintsControl.InitializeAsync(_currentUser);
                    break;
                case "FreelancerSearch":
                    await FreelancerSearchControl.InitializeAsync(_currentUser);
                    break;
            }
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

        private void Logout_Click(object sender, RoutedEventArgs e)
        { 
            App.ResetConnection();
            new StartupWindow().Show();
            Close();
        }

        // переключение тем
        private void LightThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(AppTheme.Light);
        
        private void DarkThemeButton_Click(object sender, RoutedEventArgs e) => ApplyTheme(AppTheme.Dark);
        
        private void ApplyTheme(AppTheme theme)
        {
            ThemeManager.Apply(theme);
            UpdateThemeButtons();
        }

        private void UpdateThemeButtons()
        {
            if (LightThemeButton == null || DarkThemeButton == null) return;

            LightThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Light;
            DarkThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Dark;
        }

        // переключение языков
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
