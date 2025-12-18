using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using System.Text.Json;
using FreelanceApp.Services;
using Microsoft.Win32;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class AuditLogsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private DateTime? since;
        [ObservableProperty] private DateTime? until;
        [ObservableProperty] private ObservableCollection<AuditLog> logs = [];
        [ObservableProperty] private bool isBusy;
        private const int DefaultLimit = 500;

        public AuditLogsViewModel() : this(new User { Id = 0, RoleId = 1, FirstName = "Design" }) { }
        public AuditLogsViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAsync();

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (Since is not null && Until is not null && Since > Until)
            {
                var text = Application.Current.TryFindResource("Audit_Error_InvalidRange") as string
                           ?? "Дата 'С' больше даты 'По'.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                Logs.Clear();

                DateTime? sUtc = Since is null ? null : DateTime.SpecifyKind(Since.Value, DateTimeKind.Utc);
                DateTime? uUtc = Until is null ? null : DateTime.SpecifyKind(Until.Value, DateTimeKind.Utc);

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                var list = await uow.AdminAudit.GetLogs(sUtc, uUtc, DefaultLimit);

                foreach (var row in list.OrderBy(l => l.Id))
                    Logs.Add(row);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Audit_Error_Load") as string
                           ?? "Ошибка при загрузке логов:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var text = Application.Current.TryFindResource("Audit_Warn_PathCyrillicDir") as string
                               ?? "Путь содержит кириллицу. Выберите другой каталог.";
                    var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    old_data = l.OldData?.RootElement.Clone(),
                    new_data = l.NewData?.RootElement.Clone()
                });

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                await using (var fs = File.Create(dlg.FileName))
                await JsonSerializer.SerializeAsync(fs, exportRows, opts);

                var confirmText = Application.Current.TryFindResource("Audit_Info_ExportDone") as string
                                  ?? "Экспорт завершён. Открыть папку?";
                var confirmCaption = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                var open = MessageBox.Show(confirmText, confirmCaption, MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes) Process.Start("explorer.exe", Path.GetDirectoryName(dlg.FileName)!);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Audit_Error_Export") as string
                           ?? "Ошибка экспорта:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var text = Application.Current.TryFindResource("Audit_Warn_PathCyrillicFile") as string
                               ?? "Путь содержит кириллицу. Выберите другой файл.";
                    var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                await uow.AdminAudit.ImportLogs(dlg.FileName);

                var textOk = Application.Current.TryFindResource("Audit_Info_ImportDone") as string
                             ?? "Импорт завершён.";
                var captionOk = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(textOk, captionOk, MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Audit_Error_Import") as string
                           ?? "Ошибка импорта:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool ContainsCyrillic(string path)
            => Regex.IsMatch(path, @"\p{IsCyrillic}");
    }
}
