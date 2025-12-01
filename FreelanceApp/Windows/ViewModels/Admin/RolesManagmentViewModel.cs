using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables; // User
using DAL.Models.Views;  // AdminRole
using FreelanceApp.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class RolesManagementViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // Таблица
        [ObservableProperty] private ObservableCollection<AdminRole> roles = [];
        [ObservableProperty] private AdminRole? selectedRole;

        // Состояние формы
        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isAddMode;
        [ObservableProperty] private bool isEditMode;
        [ObservableProperty] private bool isDeleteMode;

        // Привязки полей
        [ObservableProperty] private int? formId;
        [ObservableProperty] private string roleName = "";
        [ObservableProperty] private string privilegesJson = "{ }";
        [ObservableProperty] private bool isRoleNameReadOnly; // true для Edit/Delete

        public RolesManagementViewModel(User currentUser) => _currentUser = currentUser;
        public RolesManagementViewModel() : this(new User { Id = 0 }) { } // дизайн

        public async Task InitializeAsync() => await ReloadAsync();

        // ===== Загрузка
        [RelayCommand]
        private async Task ReloadAsync()
        {
            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                Roles = new ObservableCollection<AdminRole>(await uow.AdminRoles.GetRolesAsync());
                CloseForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке ролей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Режимы
        [RelayCommand]
        private void Add()
        {
            IsAddMode = true; IsEditMode = false; IsDeleteMode = false;
            IsRoleNameReadOnly = false;
            FormId = null;
            RoleName = "";
            PrivilegesJson = "{ }";
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Edit()
        {
            if (SelectedRole is null)
            {
                MessageBox.Show("Выберите роль", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsAddMode = false; IsEditMode = true; IsDeleteMode = false;
            FillForm(SelectedRole, readOnlyName: true);
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedRole is null)
            {
                MessageBox.Show("Выберите роль", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsAddMode = false; IsEditMode = false; IsDeleteMode = true;
            FillForm(SelectedRole, readOnlyName: true);
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Cancel() => CloseForm();

        // ===== Сохранение
        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                // Валидации
                if (IsAddMode && string.IsNullOrWhiteSpace(RoleName))
                {
                    MessageBox.Show("Название роли обязательно.", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Парсинг JSON с понятным сообщением
                JsonElement privs;
                try
                {
                    if (string.IsNullOrWhiteSpace(PrivilegesJson))
                        PrivilegesJson = "{ }";
                    using var doc = JsonDocument.Parse(PrivilegesJson);
                    privs = doc.RootElement.Clone();
                }
                catch (Exception)
                {
                    MessageBox.Show("Поле «Привилегии» должно содержать корректный JSON.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                if (IsAddMode)
                {
                    await uow.AdminRoles.CreateRole(_currentUser.Id, RoleName.Trim(), privs);
                }
                else if (IsEditMode)
                {
                    if (FormId is null)
                    {
                        MessageBox.Show("Не указан ID роли.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    await uow.AdminRoles.UpdateRole(_currentUser.Id, FormId.Value, privs);
                }
                else if (IsDeleteMode)
                {
                    if (FormId is null)
                    {
                        MessageBox.Show("Не указан ID роли.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (MessageBox.Show($"Удалить роль ID {FormId}?", "Подтверждение",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;

                    await uow.AdminRoles.DeleteRole(_currentUser.Id, FormId.Value);
                }

                MessageBox.Show("Операция выполнена.", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== Вспомогательные
        private void FillForm(AdminRole r, bool readOnlyName)
        {
            FormId = r.Id;
            RoleName = r.Name ?? "";
            PrivilegesJson = string.IsNullOrWhiteSpace(r.Privileges) ? "{ }" : r.Privileges;
            IsRoleNameReadOnly = readOnlyName;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            IsAddMode = IsEditMode = IsDeleteMode = false;
            FormId = null; RoleName = ""; PrivilegesJson = "{ }";
            IsRoleNameReadOnly = false;
        }
    }
}
