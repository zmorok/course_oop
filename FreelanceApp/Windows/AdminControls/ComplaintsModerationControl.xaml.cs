using System.Windows.Controls;
using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;

namespace FreelanceApp.Windows.AdminControls
{
    public partial class ComplaintsModerationControl : UserControl
    {
        public ComplaintsModerationControl() => InitializeComponent();
            
        public async Task InitializeAsync(User currentUser)
        {
            var vm = new ComplaintsModerationViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
