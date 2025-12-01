using System.Windows.Controls;
using DAL.Models.Tables;
using FreelanceApp.Windows.ViewModels;

namespace FreelanceApp.Windows.AdminControls
{
    public partial class AuditLogsControl : UserControl
    {
        public AuditLogsControl(User currentUser)
        {
            InitializeComponent();

            var vm = new AuditLogsViewModel(currentUser);
            DataContext = vm;
            _ = vm.InitializeAsync(); // первичная загрузка
        }
    }
}
