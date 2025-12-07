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
                var msg = (Application.Current.TryFindResource("Orders_Error_Load") as string ?? "Ошибка при загрузке заказов:") +
                          "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---- Команды верхних кнопок ----
        [RelayCommand]
        private void EditSelected()
        {
            if (IsArchiveView)
            {
                var text = Application.Current.TryFindResource("Orders_Error_EditArchive") as string
                           ?? "Изменение недоступно для архива.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedRow is null)
            {
                var text = Application.Current.TryFindResource("Orders_Error_SelectOrderInList") as string
                           ?? "Выберите заказ в списке.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var text = Application.Current.TryFindResource("Orders_Error_DeleteArchive") as string
                           ?? "Удаление недоступно для архива.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedRow is null)
            {
                var text = Application.Current.TryFindResource("Orders_Error_SelectOrder") as string
                           ?? "Выберите заказ.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmTextTemplate = Application.Current.TryFindResource("Orders_Confirm_DeleteOrder") as string
                                      ?? "Удалить заказ №{0}?";
            var confirmCaption = Application.Current.TryFindResource("Orders_Confirm_Caption") as string
                                 ?? "Подтверждение";
            var confirmText = string.Format(confirmTextTemplate, SelectedRow.OrderId);

            var confirm = MessageBox.Show(
                confirmText,
                confirmCaption,
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
                var msg = (Application.Current.TryFindResource("Orders_Error_Delete") as string ?? "Ошибка при удалении:") +
                          "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                var text = Application.Current.TryFindResource("Orders_Error_NoOrderForSave") as string
                           ?? "Не выбран заказ.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var status = string.IsNullOrWhiteSpace(EditStatus) ? SelectedRow.OrderStatus : EditStatus!;
            DateTime? sDeadline = EditDeadline is null ? null : DateTime.SpecifyKind(EditDeadline.Value, DateTimeKind.Utc);

            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                await uow.Orders.UpdateOrderAsync(_currentUser.Id, SelectedRow.OrderId, status, sDeadline);
                CancelEdit();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Orders_Error_Save") as string ?? "Ошибка при сохранении:") +
                          "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
