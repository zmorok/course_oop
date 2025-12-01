using System.Windows.Controls;
using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;

namespace FreelanceApp.Windows.UserControls
{
    public partial class FreelancerSearchControl : UserControl
    {
        public FreelancerSearchControl() => InitializeComponent();

        public async Task InitializeAsync(User currentUser)
        {
            var vm = new FreelancerSearchViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
