using DAL.Models.Tables;
using System.Windows.Controls;

namespace FreelanceApp.Windows.UserControls
{
    public partial class OrdersControl : UserControl
    {
        public OrdersControl() => InitializeComponent();

        public async Task InitializeAsync(User user)
        {
            var vm = new ViewModels.OrdersViewModel(user);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}