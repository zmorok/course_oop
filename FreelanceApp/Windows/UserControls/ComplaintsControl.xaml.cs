using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FreelanceApp.Windows.UserControls
{
    public partial class ComplaintsControl : UserControl
    {
        public ComplaintsControl() => InitializeComponent();

        public async Task InitializeAsync(User currentUser)
        {
            var vm = new ComplaintsViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
