using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables; // User, Role
using FreelanceApp.Services;
using Microsoft.SqlServer.Server;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class UserManagementViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // ===== Коллекции / выбор =====
        [ObservableProperty] private ObservableCollection<User> users = [];
        [ObservableProperty] private ObservableCollection<Role> roles = [];
        [ObservableProperty] private User? selectedUser;

        // ===== Состояние формы =====
        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isAddMode;
        [ObservableProperty] private bool isEditMode;
        [ObservableProperty] private bool isDeleteMode;

        // ===== Поля формы =====
        [ObservableProperty] private int? formId;
        [ObservableProperty] private string lastName = "";
        [ObservableProperty] private string firstName = "";
        [ObservableProperty] private string? middleName;
        [ObservableProperty] private string gender = "Other";
        [ObservableProperty] private string? phoneNumber;
        [ObservableProperty] private string email = "";
        [ObservableProperty] private string? password;
        [ObservableProperty] private int? selectedRoleId;
        [ObservableProperty] private decimal? rating;

        public UserManagementViewModel() : this(new User { Id = 0, RoleId = 1, FirstName = "Design" }) { } // для дизайна
        public UserManagementViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await ReloadAsync();

        // ==== Загрузка данных
        [RelayCommand]
        private async Task ReloadAsync()
        {
            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                Users = new ObservableCollection<User>(await uow.AdminUsers.GetUsersAsync());
                Roles = new ObservableCollection<Role>(await uow.AdminUsers.GetRolesAsync());

                CloseForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке пользователей: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==== Режимы
        [RelayCommand]
        private void Add()
        {
            IsAddMode = true; IsEditMode = false; IsDeleteMode = false;
            ClearForm();
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Edit()
        {
            if (SelectedUser is null)
            {
                MessageBox.Show("Выберите пользователя", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsAddMode = false; IsEditMode = true; IsDeleteMode = false;
            FillForm(SelectedUser);
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedUser is null)
            {
                MessageBox.Show("Выберите пользователя", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                IsAddMode = false; IsEditMode = false; IsDeleteMode = true;
                FormId = SelectedUser.Id;
                // остальные поля не нужны
                IsFormOpen = true;
            }
        }

        [RelayCommand]
        private void Cancel() => CloseForm();

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                


                if (IsAddMode)
                {
                    MessageBox.Show($"\tCREATE:\n" +
                    $"actorId:\t{_currentUser.Id}\n" +
                    $"password:\t{Password}\n" +
                    $"roleId:\t{SelectedRoleId}\n" +
                    $"lastName: {LastName}, firstName: {FirstName}, middleName: {MiddleName}\n" +
                    $"gender: {Gender}\n" +
                    $"phoneNumber:\t{PhoneNumber}\n" +
                    $"email:\t{Email}\n" +
                    $"rating: {Rating}");

                    await uow.AdminUsers.CreateUserAsync(
                        actorId: _currentUser.Id,
                        passwordHash: HashOrNull(Password),
                        roleId: SelectedRoleId,
                        lastName: LastName,
                        firstName: FirstName,
                        middleName: NullIfEmpty(MiddleName),
                        gender: Gender ?? "Other",
                        phoneNumber: NullIfEmpty(PhoneNumber),
                        email: Email,
                        rating: Rating ?? 0m);
                }
                else if (IsEditMode)
                {
                    if (FormId is null)
                    {
                        MessageBox.Show("Не указан ID пользователя.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    MessageBox.Show($"\tEDIT:\n" +
                        $"actorId:\t{_currentUser.Id}\n" +
                        $"userId edited:\t {FormId.Value}\n" +
                        $"password:\t{Password}\n" +
                        $"roleId:\t{SelectedRoleId}\n" +
                        $"lastName: {LastName}, firstName: {FirstName}, middleName: {MiddleName}\n" +
                        $"gender: {Gender}\n" +
                        $"phoneNumber:\t{PhoneNumber}\n" +
                        $"email:\t{Email}\n" +
                        $"rating: {Rating}");

                    await uow.AdminUsers.UpdateUserAsync(
                        actorId: _currentUser.Id,
                        userId: FormId.Value,
                        passwordHash: HashOrNull(Password),
                        roleId: SelectedRoleId,
                        lastName: LastName,
                        firstName: FirstName,
                        middleName: NullIfEmpty(MiddleName),
                        gender: Gender ?? "Other",
                        phoneNumber: NullIfEmpty(PhoneNumber),
                        email: Email,
                        rating: Rating ?? 0m);
                }
                else if (IsDeleteMode)
                {
                    if (FormId is null)
                    {
                        MessageBox.Show("Не указан ID пользователя.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (MessageBox.Show($"Удалить пользователя {FormId}?", "Подтверждение",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;

                    await uow.AdminUsers.DeleteUserAsync(
                        actorId: _currentUser.Id,
                        userId: FormId.Value);
                }

                MessageBox.Show("Операция выполнена", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==== Вспомогательные
        private void FillForm(User u)
        {
            FormId = u.Id;
            LastName = u.LastName ?? "";
            FirstName = u.FirstName ?? "";
            MiddleName = u.MiddleName;
            Gender = string.IsNullOrWhiteSpace(u.Gender) ? "Other" : u.Gender;
            PhoneNumber = u.PhoneNumber;
            Email = u.Email ?? "";
            SelectedRoleId = u.RoleId;
            Rating = u.Rating;
            Password = null; // при редактировании пароль не подставляем
        }

        private void ClearForm()
        {
            FormId = null;
            LastName = "";
            FirstName = "";
            MiddleName = null;
            Gender = "Other";
            PhoneNumber = null;
            Email = "";
            Password = null;
            SelectedRoleId = null;
            Rating = null;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            IsAddMode = IsEditMode = IsDeleteMode = false;
            ClearForm();
        }

        private static string? NullIfEmpty(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;

        private static string? HashOrNull(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return null;
            var hash = SHA512.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hash);
        }
    }
}
