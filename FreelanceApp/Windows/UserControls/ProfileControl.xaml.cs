using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FreelanceApp.Windows.UserControls 
{
    public partial class ProfileControl : UserControl
    {
        public ProfileControl()
        {
            InitializeComponent();
        }

        public async Task InitializeAsync(User user)
        {
            var vm = new ProfileViewModel(user);
            DataContext = vm;
            await vm.InitializeAsync();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProfileViewModel vm && sender is PasswordBox pb)
            {
                vm.Password = pb.Password;
            }
        }
    }
};