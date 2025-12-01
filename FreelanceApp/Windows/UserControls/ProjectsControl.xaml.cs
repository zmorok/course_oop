// ProjectsControl.xaml.cs
using System.Windows;
using System.Windows.Controls;
using FreelanceApp.Windows.ViewModels;
using DAL.Models.Tables;

namespace FreelanceApp.Windows.UserControls
{
    public partial class ProjectsControl : UserControl
    {
        public ProjectsControl() => InitializeComponent();

        public async Task InitializeAsync(User user)
        {
            var vm = new ProjectsViewModel(user);
            DataContext = vm;
            await vm.InitializeAsync();
        }
    }
}
