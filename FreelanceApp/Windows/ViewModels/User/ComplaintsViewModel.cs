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
using Microsoft.Win32;
using FreelanceApp.Helpers;

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

                    if (c.Media is not null)
                    {
                        (imageName, imageBase64) = MediaJsonHelper.ExtractFirstImage(c.Media);
                    }

                    MyComplaints.Add(new MyComplaintRow(c, imageName, imageBase64));
                }

                CounterpartOrders.Clear();
                SelectedCounterpart = null;
                SelectedOrder = null;
                SelectedMyComplaint = null;
                CloseEditor();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("ComplaintsVM_Error_Load") as string
                           ?? "Ошибка загрузки данных:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                var msg = (Application.Current.TryFindResource("ComplaintsVM_Error_LoadOrders") as string
                           ?? "Ошибка загрузки заказов контрагента:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Открыть панель — теперь с параметром выбранного заказа
        [RelayCommand]
        private void OpenCreatePanel(OrdersArchiveForComplaint? row)
        {
            if (row is null)
            {
                var text = Application.Current.TryFindResource("ComplaintsVM_Warn_SelectOrder") as string
                           ?? "Выберите заказ.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (SelectedCounterpart is null)
            {
                var text = Application.Current.TryFindResource("ComplaintsVM_Warn_SelectCounterpart") as string
                           ?? "Выберите контрагента.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedOrder = row;        // фиксируем выбор
            EditingComplaint = null;    // создаём новую
            var titleTemplate = Application.Current.TryFindResource("ComplaintsVM_Create_Title") as string
                                ?? "Жалоба на: {0}, заказ №{1}";
            PanelTitle = string.Format(titleTemplate, SelectedCounterpart.FullName, row.OrderId);
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
                var warnText = Application.Current.TryFindResource("ComplaintsVM_Warn_TextRequired") as string
                               ?? "Текст жалобы не может быть пустым.";
                var warnCaption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(warnText, warnCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        var warnText = Application.Current.TryFindResource("ComplaintsVM_Warn_SelectBoth") as string
                                       ?? "Выберите контрагента и заказ.";
                        var warnCaption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                        MessageBox.Show(warnText, warnCaption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var msg = (Application.Current.TryFindResource("ComplaintsVM_Error_Save") as string
                           ?? "Ошибка сохранения жалобы:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                var text = Application.Current.TryFindResource("ComplaintsVM_Info_EditOnlyNew") as string
                           ?? "Редактировать можно только новые жалобы.";
                var caption = Application.Current.TryFindResource("Common_Info") as string ?? "Информация";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            EditingComplaint = c;
            PanelTitle = Application.Current.TryFindResource("ComplaintsVM_Edit_Title") as string
                         ?? "Изменить жалобу";
            ComplaintText = c.Description ?? "";
            EditImageName = c.ImageName;
            EditImageBase64 = c.ImageBase64;
            EditImagePreview = MediaJsonHelper.CreateImageSource(c.ImageBase64);
            IsEditOpen = true;
        }

        [RelayCommand]
        private async Task DeleteMineAsync(MyComplaintRow? c)
        {
            var target = c ?? SelectedMyComplaint;
            if (target is null) return;

            var confirmText = Application.Current.TryFindResource("ComplaintsVM_Confirm_Delete") as string
                              ?? "Удалить жалобу?";
            var confirmCaption = Application.Current.TryFindResource("ComplaintsVM_Confirm_Caption") as string
                                 ?? "Подтверждение";

            if (MessageBox.Show(confirmText, confirmCaption,
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
                var msg = (Application.Current.TryFindResource("ComplaintsVM_Error_Delete") as string
                           ?? "Ошибка удаления жалобы:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    EditImagePreview = MediaJsonHelper.CreateImageSource(EditImageBase64);
                }
                catch (Exception ex)
                {
                    var msg = (Application.Current.TryFindResource("ComplaintsVM_Error_ReadFile") as string
                               ?? "Не удалось прочитать файл:") + " " + ex.Message;
                    var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                                  ?? "Ошибка";
                    MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    // Row-VM для "Мои жалобы"
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
        public JsonDocument? Media => _base.Media;

        public MyComplaintRow(MyComplaint @base, string? imageName, string? imageBase64)
        {
            _base = @base;
            ImageName = imageName;
            ImageBase64 = imageBase64;
        }
    }
}
