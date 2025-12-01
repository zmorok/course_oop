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
            InitializeComponent();
            _currentUser = user;
            Title = "Панель пользователя: " + user.FirstName + " " + user.LastName;
            Loaded += (_, __) =>
            {
                if (TabControlMain.SelectedItem is TabItem t) InitTab(t);
            };
            Closing += async (_, _) => await UpdateLastOnlineAsync();
        }

        private readonly HashSet<string> _initializedTabs = [];

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem t) InitTab(t);
        }

        private async void InitTab(TabItem tab)
        {
            if (tab.Header is not string header || !_initializedTabs.Add(header)) return;

            switch (header)
            {
                case "Профиль": await ProfileControl.InitializeAsync(_currentUser); break;
                case "Портфолио": await PortfolioControl.InitializeAsync(_currentUser); break;
                case "Мои заказы": await OrdersControl.InitializeAsync(_currentUser); break;
                case "Проекты": await ProjectsControl.InitializeAsync(_currentUser); break;
                case "Отзывы": await ReviewsControl.InitializeAsync(_currentUser); break;
                case "Жалобы": await ComplaintsControl.InitializeAsync(_currentUser); break;
                case "Поиск исполнителя": await FreelancerSearchControl.InitializeAsync(_currentUser); break;
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

        private void TabItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { }
    }
}
