using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;  // User
using DAL.Models.Views;   // LocalOrderDisplay
using FreelanceApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace FreelanceApp.Windows.ViewModels
{
    public enum OrderViewType { Customer, Freelancer, Archive }

    public sealed partial class OrdersViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // Список и выбор
        [ObservableProperty] private ObservableCollection<LocalOrderDisplay> orders = [];
        [ObservableProperty] private LocalOrderDisplay? selectedRow;

        // Какая вкладка выбрана (радиокнопки)
        [ObservableProperty] private OrderViewType selectedView = OrderViewType.Customer;

        // Панель редактирования
        [ObservableProperty] private bool isEditOpen;
        [ObservableProperty] private string? editStatus;     // "pending","active","completed","cancelled","disputed"
        [ObservableProperty] private DateTime? editDeadline;

        public OrdersViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAsync();

        // ---- Вычислимые свойства для XAML ----
        public bool IsCustomerView
        {
            get => SelectedView == OrderViewType.Customer;
            set { if (value) SelectedView = OrderViewType.Customer; }
        }
        public bool IsFreelancerView
        {
            get => SelectedView == OrderViewType.Freelancer;
            set { if (value) SelectedView = OrderViewType.Freelancer; }
        }
        public bool IsArchiveView
        {
            get => SelectedView == OrderViewType.Archive;
            set { if (value) SelectedView = OrderViewType.Archive; }
        }
        public bool ShowEditActions => !IsArchiveView;

        // При смене вида — перегружаем список и обновляем зависимые пропы
        partial void OnSelectedViewChanged(OrderViewType value)
        {
            IsEditOpen = false;
            OnPropertyChanged(nameof(IsCustomerView));
            OnPropertyChanged(nameof(IsFreelancerView));
            OnPropertyChanged(nameof(IsArchiveView));
            OnPropertyChanged(nameof(ShowEditActions));
            _ = LoadAsync(); // fire & forget
        }

        // ---- Загрузка ----
        private async Task LoadAsync()
        {
            Orders.Clear();
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                IEnumerable<LocalOrderDisplay> list = SelectedView switch
                {
                    OrderViewType.Customer => await uow.Orders.GetOrdersByCustomerAsync(_currentUser.Id),
                    OrderViewType.Freelancer => await uow.Orders.GetOrdersByFreelancerAsync(_currentUser.Id),
                    OrderViewType.Archive => await uow.Orders.GetArchiveOrdersAsync(_currentUser.Id),
                    _ => Array.Empty<LocalOrderDisplay>()
                };
                foreach (var o in list) Orders.Add(o);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке заказов:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Команды верхних кнопок ----
        [RelayCommand]
        private void EditSelected()
        {
            if (IsArchiveView)
            {
                MessageBox.Show("Изменение недоступно для архива.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedRow is null)
            {
                MessageBox.Show("Выберите заказ в списке.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            EditStatus = SelectedRow.OrderStatus;      // свяжется с ComboBox.SelectedValue(Tag)
            EditDeadline = SelectedRow.OrderDeadline;    // свяжется с DatePicker.SelectedDate
            IsEditOpen = true;
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (IsArchiveView)
            {
                MessageBox.Show("Удаление недоступно для архива.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedRow is null)
            {
                MessageBox.Show("Выберите заказ.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить заказ №{SelectedRow.OrderId}?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                await uow.Orders.DeleteOrderAsync(_currentUser.Id, SelectedRow.OrderId);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Команды формы ----
        [RelayCommand]
        private void CancelEdit()
        {
            IsEditOpen = false;
            EditStatus = null;
            EditDeadline = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedRow is null)
            {
                MessageBox.Show("Не выбран заказ.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var status = string.IsNullOrWhiteSpace(EditStatus) ? SelectedRow.OrderStatus : EditStatus!;
            var deadline = EditDeadline;

            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                await uow.Orders.UpdateOrderAsync(_currentUser.Id, SelectedRow.OrderId, status, deadline);
                CancelEdit();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Дополнительно — открыть редактирование по двойному клику на элемент
        [RelayCommand]
        private void OpenEditFor(LocalOrderDisplay? row)
        {
            if (row is null) return;
            SelectedRow = row;
            EditSelected();
        }
    }
}
