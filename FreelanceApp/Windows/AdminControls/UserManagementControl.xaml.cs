using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;
using System.Windows.Controls;

namespace FreelanceApp.Windows.AdminControls
{
    public partial class UserManagementControl : UserControl
    {
        public UserManagementControl() => InitializeComponent();

        public async Task InitializeAsync(User currentUser)
        {
            var vm = new UserManagementViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}