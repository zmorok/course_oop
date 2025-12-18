using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using Microsoft.Win32;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ProfileViewModel : ObservableObject
    {
        private readonly IUnitOfWork _uow;
        private readonly User _currentUser;

        [ObservableProperty] private string? firstName;
        [ObservableProperty] private string? lastName;
        [ObservableProperty] private string? middleName;
        [ObservableProperty] private string? email;
        [ObservableProperty] private string? phoneNumber;
        [ObservableProperty] private string? selectedGender;
        [ObservableProperty] private string? password;
        [ObservableProperty] private BitmapImage? photo;

        public ObservableCollection<string> Genders { get; } = ["Male", "Female", "Other"];
        public ObservableCollection<UserNotification> Notifications { get; } = [];
        public ObservableCollection<UserWarning> Warnings { get; } = [];

        public ProfileViewModel(User currentUser)
        {
            _currentUser = currentUser;
            _uow = new UnitOfWork(DbContextFactory.CreateDbContext(currentUser));

            FirstName = currentUser.FirstName;
            LastName = currentUser.LastName;
            MiddleName = currentUser.MiddleName;
            Email = currentUser.Email;
            PhoneNumber = currentUser.PhoneNumber;
            SelectedGender = currentUser.Gender;
            Photo = currentUser.Photo is { Length: > 0 } ? ToBitmap(currentUser.Photo) : null;
        }

        public async Task InitializeAsync()
        {
            await LoadNotificationsAsync();
            await LoadWarningsAsync();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                await _uow.Users.UpdateProfileAsync(
                    actorId: _currentUser.Id,
                    userId: _currentUser.Id,
                    newPasswordHash: HashOrNull(Password),
                    lastName: LastName,
                    firstName: FirstName,
                    middleName: NullIfEmpty(MiddleName),
                    gender: SelectedGender,
                    phoneNumber: NullIfEmpty(PhoneNumber),
                    email: Email,
                    photoBytes: _newPhotoBytes
                );

                var text = Application.Current.TryFindResource("Profile_Info_Saved") as string
                           ?? "Данные профиля обновлены";
                var caption = Application.Current.TryFindResource("Profile_Info_Saved_Caption") as string
                              ?? "Успешно";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);

                _currentUser.FirstName = FirstName;
                _currentUser.LastName = LastName;
                _currentUser.MiddleName = NullIfEmpty(MiddleName);
                _currentUser.Gender = SelectedGender;
                _currentUser.PhoneNumber = NullIfEmpty(PhoneNumber);
                _currentUser.Email = Email;
                if (_newPhotoBytes is null) { /* без изменений */ }
                else if (_newPhotoBytes.Length == 0) _currentUser.Photo = null;
                else _currentUser.Photo = _newPhotoBytes;

                Password = null;
                _newPhotoBytes = null;
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Profile_Error_Save") as string
                           ?? "Ошибка при сохранении профиля:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void UploadPhoto()
        {
            var ofd = new OpenFileDialog { Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp" };
            if (ofd.ShowDialog() != true) return;

            _newPhotoBytes = File.ReadAllBytes(ofd.FileName);
            Photo = ToBitmap(_newPhotoBytes);
        }

        [RelayCommand]
        private void DeletePhoto()
        {
            _newPhotoBytes = [];
            Photo = null;
        }

        [RelayCommand]
        private async Task AcceptInviteAsync(UserNotification? n)
        {
            if (n is null) return;
            try
            {
                await _uow.Users.AcceptInviteAsync(
                    actorId: _currentUser.Id,
                    notificationId: n.Id_Notification,
                    userId: _currentUser.Id
                );
                await LoadNotificationsAsync();
                var text = Application.Current.TryFindResource("Profile_Info_InviteAccepted") as string
                           ?? "Приглашение принято!";
                var caption = Application.Current.TryFindResource("Profile_Info_InviteAccepted_Caption") as string
                              ?? "Готово";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Profile_Error_Generic") as string
                           ?? "Ошибка:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeclineInviteAsync(UserNotification? n)
        {
            if (n is null) return;
            try
            {
                await _uow.Users.DeclineInviteAsync(
                    actorId: _currentUser.Id,
                    notificationId: n.Id_Notification,
                    userId: _currentUser.Id
                );
                await LoadNotificationsAsync();
                var text = Application.Current.TryFindResource("Profile_Info_InviteDeclined") as string
                           ?? "Приглашение отклонено.";
                var caption = Application.Current.TryFindResource("Profile_Info_InviteAccepted_Caption") as string
                              ?? "Готово";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Profile_Error_Generic") as string
                           ?? "Ошибка:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[]? _newPhotoBytes;

        private static BitmapImage ToBitmap(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private async Task LoadNotificationsAsync()
        {
            try
            {
                Notifications.Clear();
                var list = await _uow.Users.GetNotificationsAsync(userId: _currentUser.Id);
                foreach (var n in list) Notifications.Add(n);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Profile_Error_LoadNotifications") as string
                           ?? "Ошибка при загрузке уведомлений:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadWarningsAsync()
        {
            try
            {
                Warnings.Clear();
                var list = await _uow.Users.GetWarningsAsync(userId: _currentUser.Id);
                foreach (var w in list) Warnings.Add(w);
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Profile_Error_LoadWarnings") as string
                           ?? "Ошибка при загрузке предупреждений:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string? HashOrNull(string? password)
        {
            if (string.IsNullOrWhiteSpace(password)) return null;
            var hash = SHA512.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hash);
        }

        private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
    }
};
