using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Views;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Windows;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ComplaintsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private bool isCreateMode = true;
        [ObservableProperty] private bool isMyComplaintsMode;

        [ObservableProperty] private ObservableCollection<Counterpart> counterparts = [];
        [ObservableProperty] private ObservableCollection<OrdersArchiveForComplaint> counterpartOrders = [];
        [ObservableProperty] private ObservableCollection<MyComplaintRow> myComplaints = [];

        [ObservableProperty] private Counterpart? selectedCounterpart;
        [ObservableProperty] private OrdersArchiveForComplaint? selectedOrder;
        [ObservableProperty] private MyComplaintRow? selectedMyComplaint;

        [ObservableProperty] private bool isEditOpen;
        [ObservableProperty] private string panelTitle = "";
        [ObservableProperty] private string complaintText = "";
        [ObservableProperty] private MyComplaintRow? editingComplaint;

        [ObservableProperty] private string? editImageName;
        [ObservableProperty] private string? editImageBase64;
        [ObservableProperty] private ImageSource? editImagePreview;

        public bool HasEditImage => EditImagePreview is not null;

        partial void OnEditImagePreviewChanged(ImageSource? value)
        {
            OnPropertyChanged(nameof(HasEditImage));
        }


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
                var baseComplaints = await uow.Complaints.GetComplaintsAsync(_currentUser.Id);
                foreach (var c in baseComplaints)
                {
                    string? imageName = null;
                    string? imageBase64 = null;
                    ImageSource? imageSource = null;

                    if (c.Media is not null)
                    {
                        (imageName, imageBase64) = MediaFromJson(c.Media);
                        imageSource = CreateImageSource(imageBase64);
                    }

                    MyComplaints.Add(new MyComplaintRow(c, imageName, imageBase64, imageSource));
                }

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

            SelectedOrder = row;        // фиксируем выбор
            EditingComplaint = null;    // создаём новую
            PanelTitle = $"Жалоба на: {SelectedCounterpart.FullName}, заказ №{row.OrderId}";
            ComplaintText = "";
            EditImageName = "";
            EditImageBase64 = "";
            EditImagePreview = null;
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

                var imageBase64 = (EditImageBase64 ?? "").Trim();
                var hasImage = !string.IsNullOrWhiteSpace(imageBase64);

                if (EditingComplaint is not null)
                {
                    string mediaJsonUpdate;
                    if (hasImage)
                    {
                        var media = new[]
                        {
                            new
                            {
                                type = "image",
                                name = EditImageName ?? "image",
                                content = imageBase64
                            }
                        };
                        mediaJsonUpdate = JsonSerializer.Serialize(media);
                    }
                    else
                    {
                        mediaJsonUpdate = "[]";
                    }

                    await uow.Complaints.UpdateComplaintAsync(
                        actorId: _currentUser.Id,
                        complaintId: EditingComplaint.Id_Complaint,
                        description: text,
                        mediaJson: mediaJsonUpdate);
                }
                else
                {
                    if (SelectedCounterpart is null || SelectedOrder is null)
                    {
                        MessageBox.Show("Выберите контрагента и заказ.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string? mediaJsonCreate = null;
                    if (hasImage)
                    {
                        var media = new[]
                        {
                            new
                            {
                                type = "image",
                                name = EditImageName ?? "image",
                                content = imageBase64
                            }
                        };
                        mediaJsonCreate = JsonSerializer.Serialize(media);
                    }

                    await uow.Complaints.CreateComplaintAsync(
                        actorId: _currentUser.Id,
                        filedById: _currentUser.Id,
                        targetUserId: SelectedCounterpart.Id,
                        description: text,
                        mediaJson: mediaJsonCreate,
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
            EditImageName = "";
            EditImageBase64 = "";
            EditImagePreview = null;
        }

        // Мои жалобы
        [RelayCommand]
        private void EditMine(MyComplaintRow? c)
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
            EditImageName = c.ImageName;
            EditImageBase64 = c.ImageBase64;
            EditImagePreview = c.ImageSource;
            IsEditOpen = true;
        }

        [RelayCommand]
        private async Task DeleteMineAsync(MyComplaintRow? c)
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

        [RelayCommand]
        private void SelectImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Выберите изображение для жалобы"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dialog.FileName);
                    EditImageBase64 = Convert.ToBase64String(bytes);
                    EditImageName = Path.GetFileName(dialog.FileName);
                    EditImagePreview = CreateImageSource(EditImageBase64);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Не удалось прочитать файл: {ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ClearImage()
        {
            EditImageBase64 = "";
            EditImageName = "";
            EditImagePreview = null;
        }

        // ===== Работа с медиа (jsonb) =====
        private static (string? name, string? base64) MediaFromJson(JsonDocument doc)
        {
            try
            {
                var root = doc.RootElement;

                // Основной вариант – массив, как в портфолио/проектах
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in root.EnumerateArray())
                    {
                        if (el.TryGetProperty("type", out var typeProp)
                            && string.Equals(typeProp.GetString(), "image", StringComparison.OrdinalIgnoreCase)
                            && el.TryGetProperty("content", out var contentProp))
                        {
                            var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                            return (name, contentProp.GetString());
                        }
                    }
                }
                // На всякий случай поддержим и объект в корне (если в БД медиа лежит как один объект)
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    var el = root;
                    if (el.TryGetProperty("type", out var typeProp)
                        && string.Equals(typeProp.GetString(), "image", StringComparison.OrdinalIgnoreCase)
                        && el.TryGetProperty("content", out var contentProp))
                    {
                        var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        return (name, contentProp.GetString());
                    }
                }
            }
            catch
            {
            }

            return (null, null);
        }

        private static ImageSource? CreateImageSource(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var bitmap = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }

    // Row-VM для "Мои жалобы" с картинкой
    public sealed class MyComplaintRow
    {
        private readonly MyComplaint _base;

        public int Id_Complaint => _base.Id_Complaint;
        public int Id_User => _base.Id_User;
        public int Filed_By => _base.Filed_By;
        public string TargetName
        {
            get => _base.TargetName;
            set => _base.TargetName = value;
        }
        public string Status => _base.Status;
        public string Description => _base.Description;
        public string DescriptionPreview => _base.DescriptionPreview;
        public short ChangeCounter => _base.ChangeCounter;
        public short RemainingChanges => _base.RemainingChanges;
        public bool IsEditable => _base.IsEditable;
        public bool CanEditMore => _base.CanEditMore;

        public string? ImageName { get; }
        public string? ImageBase64 { get; }
        public ImageSource? ImageSource { get; }
        public bool HasImage => ImageSource is not null;

        public MyComplaintRow(MyComplaint @base, string? imageName, string? imageBase64, ImageSource? imageSource)
        {
            _base = @base;
            ImageName = imageName;
            ImageBase64 = imageBase64;
            ImageSource = imageSource;
        }
    }
}
