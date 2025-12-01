using System.Threading.Tasks;
using System.Windows.Controls;
using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;

namespace FreelanceApp.Windows.UserControls
{
    public partial class PortfolioControl : UserControl
    {
        public PortfolioControl() => InitializeComponent();

        public async Task InitializeAsync(User user)
        {
            var vm = new PortfolioViewModel(user);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
