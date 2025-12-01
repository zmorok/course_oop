using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Views;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Windows;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ComplaintsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private bool isCreateMode = true;
        [ObservableProperty] private bool isMyComplaintsMode;

        [ObservableProperty] private ObservableCollection<Counterpart> counterparts = [];
        [ObservableProperty] private ObservableCollection<OrdersArchiveForComplaint> counterpartOrders = [];
        [ObservableProperty] private ObservableCollection<MyComplaint> myComplaints = [];

        [ObservableProperty] private Counterpart? selectedCounterpart;
        [ObservableProperty] private OrdersArchiveForComplaint? selectedOrder;
        [ObservableProperty] private MyComplaint? selectedMyComplaint;

        [ObservableProperty] private bool isEditOpen;
        [ObservableProperty] private string panelTitle = "";
        [ObservableProperty] private string complaintText = "";
        [ObservableProperty] private MyComplaint? editingComplaint;


        public ComplaintsViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAllAsync();

        // Закрываем панель при переключении режимов
        partial void OnIsCreateModeChanged(bool value)
        {
            if (value && IsMyComplaintsMode) IsMyComplaintsMode = false;
            CloseEditor();
        }
        partial void OnIsMyComplaintsModeChanged(bool value)
        {
            if (value && IsCreateMode) IsCreateMode = false;
            CloseEditor();
        }

        private async Task LoadAllAsync()
        {
            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                Counterparts.Clear();
                foreach (var u in await uow.Complaints.GetCounterpartsAsync(_currentUser.Id))
                    Counterparts.Add(u);

                MyComplaints.Clear();
                foreach (var c in await uow.Complaints.GetComplaintsAsync(_currentUser.Id))
                    MyComplaints.Add(c);

                CounterpartOrders.Clear();
                SelectedCounterpart = null;
                SelectedOrder = null;
                SelectedMyComplaint = null;
                CloseEditor();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Подгрузка заказов при выборе контрагента
        partial void OnSelectedCounterpartChanged(Counterpart? value)
        {
            _ = LoadOrdersForCounterpartAsync(value);
        }

        private async Task LoadOrdersForCounterpartAsync(Counterpart? cp)
        {
            CounterpartOrders.Clear();
            SelectedOrder = null;
            CloseEditor();
            if (cp is null) return;

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                var rows = await uow.Orders.GetArchiveOrdersCounterpartsAsync(_currentUser.Id);
                foreach (var r in rows.Where(o => o.FreelancerId == cp.Id || o.CustomerId == cp.Id))
                    CounterpartOrders.Add(r);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заказов контрагента: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Открыть панель — теперь с параметром выбранного заказа
        [RelayCommand]
        private void OpenCreatePanel(OrdersArchiveForComplaint? row)
        {
            if (row is null)
            {
                MessageBox.Show("Выберите заказ.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedCounterpart is null)
            {
                MessageBox.Show("Выберите контрагента.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedOrder = row;      // фиксируем выбор
            EditingComplaint = null;    // создаём новую
            PanelTitle = $"Жалоба на: {SelectedCounterpart.FullName}, заказ №{row.OrderId}";
            ComplaintText = "";
            IsEditOpen = true;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            var text = (ComplaintText ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Текст жалобы не может быть пустым.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                if (EditingComplaint is not null)
                {
                    await uow.Complaints.UpdateComplaintAsync(
                        actorId: _currentUser.Id,
                        complaintId: EditingComplaint.Id_Complaint,
                        description: text);
                }
                else
                {
                    if (SelectedCounterpart is null || SelectedOrder is null)
                    {
                        MessageBox.Show("Выберите контрагента и заказ.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await uow.Complaints.CreateComplaintAsync(
                        actorId: _currentUser.Id,
                        filedById: _currentUser.Id,
                        targetUserId: SelectedCounterpart.Id,
                        description: text,
                        orderArchiveId: SelectedOrder.OrderArcId);
                }

                CloseEditor();
                await LoadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения жалобы: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel() => CloseEditor();

        private void CloseEditor()
        {
            IsEditOpen = false;
            PanelTitle = "";
            ComplaintText = "";
            EditingComplaint = null;
        }

        // Мои жалобы
        [RelayCommand]
        private void EditMine(MyComplaint? c)
        {
            if (c is null) return;
            if (!c.IsEditable)
            {
                MessageBox.Show("Редактировать можно только новые жалобы.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EditingComplaint = c;
            PanelTitle = "Изменить жалобу";
            ComplaintText = c.Description ?? "";
            IsEditOpen = true;
        }

        [RelayCommand]
        private async Task DeleteMineAsync(MyComplaint? c)
        {
            var target = c ?? SelectedMyComplaint;
            if (target is null) return;

            if (MessageBox.Show("Удалить жалобу?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                await uow.Complaints.DeleteComplaintAsync(
                    actorId: _currentUser.Id,
                    complaintId: target.Id_Complaint);

                await LoadAllAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления жалобы: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
