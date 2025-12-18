using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
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

        [ObservableProperty] private ObservableCollection<User> users = [];
        [ObservableProperty] private ObservableCollection<Role> roles = [];
        [ObservableProperty] private User? selectedUser;

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isAddMode;
        [ObservableProperty] private bool isEditMode;
        [ObservableProperty] private bool isDeleteMode;

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

        public UserManagementViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await ReloadAsync();

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
                var msg = (Application.Current.TryFindResource("AdminUsers_Error_Load") as string
                           ?? "Ошибка при загрузке пользователей:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                var text = Application.Current.TryFindResource("AdminUsers_Warn_SelectUser") as string
                           ?? "Выберите пользователя.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var text = Application.Current.TryFindResource("AdminUsers_Warn_SelectUser") as string
                           ?? "Выберите пользователя.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                IsAddMode = false; IsEditMode = false; IsDeleteMode = true;
                FormId = SelectedUser.Id;
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
                        var text = Application.Current.TryFindResource("AdminUsers_Error_NoUserId") as string
                                   ?? "Не указан ID пользователя.";
                        var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                                      ?? "Ошибка";
                        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

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
                        var text = Application.Current.TryFindResource("AdminUsers_Error_NoUserId") as string
                                   ?? "Не указан ID пользователя.";
                        var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                                      ?? "Ошибка";
                        MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var confirmTemplate = Application.Current.TryFindResource("AdminUsers_Confirm_DeleteUser") as string
                                          ?? "Удалить пользователя {0}?";
                    var confirmCaption = Application.Current.TryFindResource("AdminUsers_Confirm_Caption") as string
                                         ?? "Подтверждение";
                    var confirmText = string.Format(confirmTemplate, FormId);

                    if (MessageBox.Show(confirmText, confirmCaption,
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;

                    await uow.AdminUsers.DeleteUserAsync(
                        actorId: _currentUser.Id,
                        userId: FormId.Value);
                }

                var okText = Application.Current.TryFindResource("AdminUsers_Info_OperationDone") as string
                             ?? "Операция выполнена";
                var okCaption = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(okText, okCaption, MessageBoxButton.OK, MessageBoxImage.Information);

                await ReloadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("AdminUsers_Error_Generic") as string ?? "Ошибка:")
                           + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            Password = null;
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
