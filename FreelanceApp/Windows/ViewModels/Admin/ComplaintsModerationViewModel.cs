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

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ComplaintsModerationViewModel : ObservableObject
    {
        private readonly User _currentUser;
        private bool _isReady;

        // ===== Фильтр статусов (для верхнего комбобокса)
        public sealed record StatusFilter(string Title, string Mode, string? ExactStatus);
        public ObservableCollection<StatusFilter> StatusFilters { get; } =
        [
            new("Новые",        "unsolved", "new"),
            new("В работе",     "unsolved", "in_progress"),
            new("Решённые",     "resolved", "resolved"),
            new("Отклонённые",  "unsolved", "dismissed"),
            new("Все",          "all",       null),
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
        public string[] StatusEditOptions { get; } = ["new", "in_progress", "resolved", "dismissed"];
        [ObservableProperty] private string? statusEdit;

        // ===== Прочее
        [ObservableProperty] private string warningText = "";
        [ObservableProperty] private string selectedMediaPretty = "";
        [ObservableProperty] private bool isBusy;

        public ComplaintsModerationViewModel() : this(new User { Id = 0, RoleId = 1, FirstName = "Design" }) { }
        public ComplaintsModerationViewModel(User currentUser)
        {
            _currentUser = currentUser;
            SelectedStatusFilter = StatusFilters[0]; // по умолчанию «Новые»
        }

        public async Task InitializeAsync() { _isReady = true; await RefreshAsync(); }

        // обновляем правую панель при выборе строки
        partial void OnSelectedComplaintChanged(AdminComplaint? value)
        {
            StatusEdit = value?.Status;
            WarningText = "";
            SelectedMediaPretty = Pretty(value?.Media);
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
                MessageBox.Show($"Ошибка загрузки: {ex.InnerException?.Message ?? ex.Message}",
                    "Жалобы", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка изменения статуса: {ex.InnerException?.Message ?? ex.Message}",
                    "Жалобы", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Ошибка закрытия жалобы: {ex.InnerException?.Message ?? ex.Message}",
                    "Жалобы", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Укажи причину предупреждения.", "Предупреждение",
                        MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Ошибка выдачи предупреждения: {ex.InnerException?.Message ?? ex.Message}",
                    "Жалобы", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Utils
        private static string Pretty(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { return json; }
        }
    }
}
