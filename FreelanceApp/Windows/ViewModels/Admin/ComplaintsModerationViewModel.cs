using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Context;
using DAL.Models.Tables;      // User
using DAL.Models.Views;       // AdminComplaint (ваша проекция из v_admin_complaints)
using DAL.Repository.AdminRepositories;
using FreelanceApp.Services;
using System.Windows.Data;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ComplaintsModerationViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private bool _isReady;

        // ===== Фильтр статусов (для верхнего комбобокса)
        // В модели храним только код статуса (Code); человекочитаемый текст берём из ресурсных словарей.
        public sealed record StatusFilter(string Code, string Mode, string? ExactStatus)
        {
            public override string ToString()
            {
                string resourceKey = Code switch
                {
                    "new"         => "AdminComplaints_Filter_Status_New",
                    "in_progress" => "AdminComplaints_Filter_Status_InProgress",
                    "resolved"    => "AdminComplaints_Filter_Status_Resolved",
                    "dismissed"   => "AdminComplaints_Filter_Status_Dismissed",
                    "all"         => "AdminComplaints_Filter_Status_All",
                    _             => Code
                };

                var localized = Application.Current.TryFindResource(resourceKey) as string;
                return localized ?? Code;
            }
        }
        public ObservableCollection<StatusFilter> StatusFilters { get; } =
        [
            new("new",        "unsolved", "new"),
            new("in_progress","unsolved", "in_progress"),
            new("resolved",   "resolved", "resolved"),
            // Для отклонённых берём все из БД и фильтруем по точному статусу,
            // иначе репозиторий в режиме "unsolved" их не вернёт.
            new("dismissed",  "all",      "dismissed"),
            new("all",        "all",       null),
        ];

        [ObservableProperty] private StatusFilter selectedStatusFilter;
        partial void OnSelectedStatusFilterChanged(StatusFilter value)
        {
            if (!_isReady) return;
            _ = RefreshAsync();     // fire-and-forget, чтобы не блокировать UI
        }

        [ObservableProperty] private string searchText = "";

        // ===== Данные списка
        [ObservableProperty] private ObservableCollection<AdminComplaint> complaints = [];
        [ObservableProperty] private AdminComplaint? selectedComplaint;

        // ===== Правый блок (редактирование статуса)
        // Здесь тоже храним только код статуса; ToString возвращает локализованный текст для выбранного значения.
        public sealed record StatusEditOption(string Code)
        {
            public override string ToString()
            {
                string resourceKey = Code switch
                {
                    "new"         => "AdminComplaints_Status_New",
                    "in_progress" => "AdminComplaints_Status_InProgress",
                    "resolved"    => "AdminComplaints_Status_Resolved",
                    "dismissed"   => "AdminComplaints_Status_Dismissed",
                    _             => Code
                };

                var localized = Application.Current.TryFindResource(resourceKey) as string;
                return localized ?? Code;
            }
        }
        public IReadOnlyList<StatusEditOption> StatusEditOptions { get; } =
        [
            new("new"),
            new("in_progress"),
            new("resolved"),
            new("dismissed")
        ];
        [ObservableProperty] private string? statusEdit;

        // ===== Прочее
        [ObservableProperty] private string warningText = "";
        [ObservableProperty] private bool isBusy;

        public ComplaintsModerationViewModel() : this(new User { Id = 0, RoleId = 1, FirstName = "Design" }) { }
        public ComplaintsModerationViewModel(User currentUser)
        {
            _currentUser = currentUser;
            SelectedStatusFilter = StatusFilters[0]; // по умолчанию «Новые»
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        public async Task InitializeAsync() { _isReady = true; await RefreshAsync(); }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // Обновляем коллекции и выбранные значения, чтобы ComboBox пересчитал отображение
            CollectionViewSource.GetDefaultView(StatusFilters)?.Refresh();
            CollectionViewSource.GetDefaultView(StatusEditOptions)?.Refresh();

            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(StatusEdit));
        }

        // Вызывается MVVM Toolkit при изменении StatusEdit (кода статуса).
        // Можно использовать как триггер для обновления связанных свойств/привязок.
        partial void OnStatusEditChanged(string? value)
        {
            // На всякий случай уведомляем об изменении самого свойства,
            // чтобы все привязки, использующие StatusEdit, перерисовались.
            OnPropertyChanged(nameof(StatusEdit));
        }

        // обновляем правую панель при выборе строки
        partial void OnSelectedComplaintChanged(AdminComplaint? value)
        {
            StatusEdit = value?.Status;
            WarningText = "";
        }

        // ===== Команды

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                Complaints.Clear();

                await using var ctx = DbContextFactory.CreateDbContext(_currentUser);
                var repo = new AdminModerationRepository(ctx);

                // 1) базовый набор по «режиму» (all | unsolved | resolved)
                var rows = await repo.GetComplaintsAsync(SelectedStatusFilter.Mode);

                // 2) точный статус (для «Новые», «В работе», «Отклонённые»)
                if (!string.IsNullOrWhiteSpace(SelectedStatusFilter.ExactStatus))
                    rows = rows.Where(r => r.Status == SelectedStatusFilter.ExactStatus).ToList();

                // 3) клиентский поиск
                var q = (SearchText ?? "").Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(q))
                {
                    rows = rows.Where(r =>
                        (r.Description ?? "").ToLowerInvariant().Contains(q) ||
                        (r.FiledByName ?? "").ToLowerInvariant().Contains(q) ||
                        (r.UserComName ?? "").ToLowerInvariant().Contains(q))
                        .ToList();
                }

                foreach (var r in rows) Complaints.Add(r);
                SelectedComplaint = null;
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("AdminComplaintsVM_Error_Load") as string
                           ?? "Ошибка загрузки:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Жалобы";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task SaveStatusAsync()
        {
            if (SelectedComplaint is null || string.IsNullOrWhiteSpace(StatusEdit)) return;

            try
            {
                await using var ctx = DbContextFactory.CreateDbContext(_currentUser);
                var repo = new AdminModerationRepository(ctx);

                await repo.UpdateComplaintStatusAsync(
                    actorId: _currentUser.Id,
                    complaintId: SelectedComplaint.Id_Complaint,
                    newStatus: StatusEdit!,
                    adminId: _currentUser.Id);

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("AdminComplaintsVM_Error_SaveStatus") as string
                           ?? "Ошибка изменения статуса:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Жалобы";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task TakeInWorkAsync()
        {
            StatusEdit = "in_progress";
            await SaveStatusAsync();
        }

        [RelayCommand]
        private async Task DismissAsync()
        {
            StatusEdit = "dismissed";
            await SaveStatusAsync();
        }

        [RelayCommand]
        private async Task ResolveAsync()
        {
            try
            {
                if (SelectedComplaint is null) return;

                await using var ctx = DbContextFactory.CreateDbContext(_currentUser);
                var repo = new AdminModerationRepository(ctx);

                await repo.ResolveComplaintAsync(
                    actorId: _currentUser.Id,
                    complaintId: SelectedComplaint.Id_Complaint,
                    resolutionStatus: "resolved",
                    adminId: _currentUser.Id);

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("AdminComplaintsVM_Error_Resolve") as string
                           ?? "Ошибка закрытия жалобы:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Жалобы";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task IssueWarningAsync()
        {
            try
            {
                if (SelectedComplaint is null)
                    return;

                var reason = (WarningText ?? "").Trim();
                if (string.IsNullOrWhiteSpace(reason))
                {
                    var text = Application.Current.TryFindResource("AdminComplaintsVM_Warn_ReasonRequired") as string
                               ?? "Укажи причину предупреждения.";
                    var caption = Application.Current.TryFindResource("AdminComplaintsVM_Warn_ReasonCaption") as string
                                  ?? "Предупреждение";
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await using var ctx = DbContextFactory.CreateDbContext(_currentUser);
                var repo = new AdminModerationRepository(ctx);

                await repo.IssueWarningAsync(
                    actorId: _currentUser.Id,
                    complaintId: SelectedComplaint.Id_Complaint,
                    targetUserId: SelectedComplaint.UserComId,
                    message: reason,
                    expiresDays: 7);

                // опционально — сразу закрыть как resolved
                await repo.ResolveComplaintAsync(
                    actorId: _currentUser.Id,
                    complaintId: SelectedComplaint.Id_Complaint,
                    resolutionStatus: "resolved",
                    adminId: _currentUser.Id);

                WarningText = "";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("AdminComplaintsVM_Error_IssueWarning") as string
                           ?? "Ошибка выдачи предупреждения:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Жалобы";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        
    }
}
