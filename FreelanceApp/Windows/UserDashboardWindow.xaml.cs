using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FreelanceApp.Authentication;
using DAL.Models.Tables;
using FreelanceApp.Services;
using FreelanceApp.Windows.UserControls;
using Microsoft.EntityFrameworkCore;

namespace FreelanceApp.Windows
{
    public partial class UserDashboardWindow : Window
    {
        private readonly User _currentUser;

        public UserDashboardWindow(User user)
        {
            _currentUser = user;

            var baseTitle = Application.Current.TryFindResource("UserDashboard_Title") as string ?? "Панель пользователя";
            Title = $"{baseTitle}: {user.FirstName} {user.LastName}";

            Loaded += (_, __) =>
            {
                if (TabControlMain.SelectedItem is TabItem t) InitTab(t);
                UpdateThemeButtons();
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
                ex = ex.InnerException ?? ex;
                MessageBox.Show(
                    "Ошибка обновления статуса онлайн:\n\n" + $"{ex.Message}\n\n",
                    "Ошибка обновления статуса онлайн",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
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
            if (LightThemeButton == null || DarkThemeButton == null) return;

            LightThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Light;
            DarkThemeButton.IsEnabled = ThemeManager.CurrentTheme != AppTheme.Dark;
        }

        private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
    }
}
