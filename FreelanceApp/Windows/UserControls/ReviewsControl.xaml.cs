using DAL;
using DAL.Models.Tables;
using DAL.Models.Views;
using FreelanceApp.Services;
using FreelanceApp.Windows.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FreelanceApp.Windows.UserControls
{
    public partial class ReviewsControl : UserControl
    {
        public ReviewsControl() => InitializeComponent();
        
        public async Task InitializeAsync(User currentUser)
        {
            var vm = new ReviewsViewModel(currentUser);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
