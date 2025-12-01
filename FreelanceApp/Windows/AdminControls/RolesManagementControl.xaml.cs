using System.Threading.Tasks;
using System.Windows.Controls;
using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;

namespace FreelanceApp.Windows.AdminControls
{
    public partial class RolesManagementControl : UserControl
    {
        public RolesManagementControl() => InitializeComponent();

        public async Task InitializeAsync(User currentUser)
        {
            var vm = new RolesManagementViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
