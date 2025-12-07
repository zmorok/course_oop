using System.Windows;
using FreelanceApp.Services;

namespace FreelanceApp.Windows
{
    public partial class UserDashboardWindow
    {
        private void SetRu_Click(object sender, RoutedEventArgs e) =>
            LocalizationManager.SetLanguage(AppLanguage.Ru);

        private void SetEn_Click(object sender, RoutedEventArgs e) =>
            LocalizationManager.SetLanguage(AppLanguage.En);
    }
}

