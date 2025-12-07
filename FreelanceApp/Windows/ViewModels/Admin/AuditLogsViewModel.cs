using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;   // User
using System.Text.Json;    // AuditLog (класс вашей вьюхи)
using FreelanceApp.Services;
using Microsoft.Win32;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class AuditLogsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // ===== Состояние/данные
        [ObservableProperty] private DateTime? since;
        [ObservableProperty] private DateTime? until;
        [ObservableProperty] private ObservableCollection<AuditLog> logs = [];
        [ObservableProperty] private bool isBusy;
        private const int DefaultLimit = 500;

        public AuditLogsViewModel() : this(new User { Id = 0, RoleId = 1, FirstName = "Design" }) { }
        public AuditLogsViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAsync();

        // ===== Команды
        [RelayCommand]
        private async Task LoadAsync()
        {
            // простая валидация диапазона
            if (Since is not null && Until is not null && Since > Until)
            {
                MessageBox.Show("Дата 'С' больше даты 'По'.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                Logs.Clear();

                // Нормализуем в UTC (DatePicker даёт Kind=Unspecified)
                DateTime? sUtc = Since is null ? null : DateTime.SpecifyKind(Since.Value, DateTimeKind.Utc);
                DateTime? uUtc = Until is null ? null : DateTime.SpecifyKind(Until.Value, DateTimeKind.Utc);

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                var list = await uow.AdminAudit.GetLogs(sUtc, uUtc, DefaultLimit);

                foreach (var row in list.OrderBy(l => l.Id))
                    Logs.Add(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке логов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    FileName = "audit_logs.json"
                };
                if (dlg.ShowDialog() != true) return;

                if (ContainsCyrillic(dlg.FileName))
                {
                    MessageBox.Show("Путь содержит кириллицу. Выберите другой каталог.",
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsBusy = true;

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                DateTime? sUtc = Since is null ? null : DateTime.SpecifyKind(Since.Value, DateTimeKind.Utc);
                DateTime? uUtc = Until is null ? null : DateTime.SpecifyKind(Until.Value, DateTimeKind.Utc);
                var rows = await uow.AdminAudit.GetLogs(sUtc, uUtc, DefaultLimit);

                var exportRows = rows.Select(l => new
                {
                    id = l.Id,
                    user_id = l.UserId,
                    proc_name = l.ProcName,
                    action = l.Action,
                    table_name = l.TableName,
                    record_id = l.RecordId,
                    changed_at = DateTime.SpecifyKind(l.ChangedAt, DateTimeKind.Utc),
                    // клонируем JsonElement, чтобы не зависеть от жизненного цикла JsonDocument
                    old_data = l.OldData?.RootElement.Clone(),
                    new_data = l.NewData?.RootElement.Clone()
                });

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                await using (var fs = File.Create(dlg.FileName))
                await JsonSerializer.SerializeAsync(fs, exportRows, opts);

                var open = MessageBox.Show("Экспорт завершён. Открыть папку?", "Успех", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes) Process.Start("explorer.exe", Path.GetDirectoryName(dlg.FileName)!);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        private async Task ImportAsync()
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
                };
                if (dlg.ShowDialog() != true) return;

                if (ContainsCyrillic(dlg.FileName))
                {
                    MessageBox.Show("Путь содержит кириллицу. Выберите другой файл.", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                await uow.AdminAudit.ImportLogs(dlg.FileName);

                MessageBox.Show("Импорт завершён.", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync(); // перезагрузить после импорта
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool ContainsCyrillic(string path)
            => Regex.IsMatch(path, @"\p{IsCyrillic}");
    }
}
