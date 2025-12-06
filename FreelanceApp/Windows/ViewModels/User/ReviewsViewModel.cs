using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ReviewsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // ===== коллекция в списке
        [ObservableProperty] private ObservableCollection<ReviewRow> rows = [];
        [ObservableProperty] private ReviewRow? selectedRow;

        // ===== режим просмотра (я — заказчик/исполнитель)
        [ObservableProperty] private bool asCustomer = true;
        [ObservableProperty] private bool asFreelancer;

        // переключатели взаимно-исключающие + автоперезагрузка
        partial void OnAsCustomerChanged(bool value)
        {
            if (value)
            {
                if (AsFreelancer) AsFreelancer = false;
                _ = LoadAsync();
            }
        }
        partial void OnAsFreelancerChanged(bool value)
        {
            if (value)
            {
                if (AsCustomer) AsCustomer = false;
                _ = LoadAsync();
            }
        }

        // ===== панель редактирования
        [ObservableProperty] private bool isEditOpen;
        [ObservableProperty] private string panelTitle = "";
        [ObservableProperty] private int editRating = 5;       // 1..5
        [ObservableProperty] private string editComment = "";
        [ObservableProperty] private string? editImageName;
        [ObservableProperty] private string? editImageBase64;
        [ObservableProperty] private ImageSource? editImagePreview;

        // Есть ли превью изображения для текущего редактируемого отзыва
        public bool HasEditImage => EditImagePreview is not null;

        partial void OnEditImagePreviewChanged(ImageSource? value)
        {
            OnPropertyChanged(nameof(HasEditImage));
        }

        public ReviewsViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            try
            {
                Rows.Clear();
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                // получаем строки «заказы из архива + мои/их отзывы» (включая медиа) через view
                var items = await uow.Reviews.GetOrderReviewsAsync(_currentUser.Id, AsCustomer);

                foreach (var r in items)
                {
                    // определяем, кем я был в заказе по факту:
                    bool iAmCustomer = _currentUser.Id == r.Id_Customer;

                    string otherName = iAmCustomer
                        ? (r.Freelancer_Fullname ?? "[удалён]")
                        : r.Customer_Fullname;

                    var myComment = iAmCustomer ? r.Customer_Comment : r.Freelancer_Comment;
                    var myRating = iAmCustomer ? r.Customer_Rating : r.Freelancer_Rating;
                    var myReviewId = iAmCustomer ? r.Customer_Review_Id : r.Freelancer_Review_Id;

                    var oppComment = iAmCustomer ? r.Freelancer_Comment : r.Customer_Comment;
                    var oppRating = iAmCustomer ? r.Freelancer_Rating : r.Customer_Rating;

                    // медиа берём напрямую из v_orders_reviews (customer_media / freelancer_media)
                    var myMediaDoc = iAmCustomer ? r.Customer_Media : r.Freelancer_Media;
                    var oppMediaDoc = iAmCustomer ? r.Freelancer_Media : r.Customer_Media;

                    var (myImageName, myImageBase64) = MediaFromJson(myMediaDoc);
                    var myImageSource = CreateImageSource(myImageBase64);

                    var (oppImageName, oppBase64) = MediaFromJson(oppMediaDoc);
                    var oppImageSource = CreateImageSource(oppBase64);

                    Rows.Add(new ReviewRow(
                        orderId: r.Order_Id,
                        projectTitle: r.Project_Title,
                        otherSideName: otherName,
                        reviewId: myReviewId,
                        myComment: myComment,
                        myRating: myRating,
                        oppComment: oppComment,
                        oppRating: oppRating,
                        myImageName: myImageName,
                        myImageBase64: myImageBase64,
                        myImageSource: myImageSource,
                        oppImageName: oppImageName,
                        oppImageSource: oppImageSource
                    ));
                }

                // закрыть форму, сбросить выбор
                IsEditOpen = false;
                SelectedRow = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки отзывов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // открыть форму для добавления/редактирования
        [RelayCommand]
        private void OpenEditFor(ReviewRow? row)
        {
            if (row is null) return;
            SelectedRow = row;

            PanelTitle = row.ReviewId is null ? "Новый отзыв" : "Изменить отзыв";
            EditComment = row.MyComment ?? "";
            EditRating = (row.MyRating is >= 1 and <= 5) ? row.MyRating.Value : 5;
            EditImageName = row.MyImageName;
            EditImageBase64 = row.MyImageBase64;
            EditImagePreview = row.MyImageSource;

            IsEditOpen = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditOpen = false;
            EditComment = "";
            EditRating = 5;
            EditImageName = "";
            EditImageBase64 = "";
            EditImagePreview = null;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (SelectedRow is null) return;

            var comment = (EditComment ?? "").Trim();
            if (string.IsNullOrWhiteSpace(comment))
            {
                MessageBox.Show("Комментарий не может быть пустым.",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var rating = Math.Clamp(EditRating, 1, 5);
            var imageBase64 = (EditImageBase64 ?? "").Trim();
            var hasImage = !string.IsNullOrWhiteSpace(imageBase64);

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                if (SelectedRow.ReviewId is null)
                {
                    // Создание нового отзыва: если картинки нет — вообще не передаём медиа (останется NULL)
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

                    await uow.Reviews.CreateReviewAsync(
                        actorId: _currentUser.Id,
                        orderId: SelectedRow.OrderId,
                        reviewerId: _currentUser.Id,
                        comment: comment,
                        rating: rating,
                        mediaJson: mediaJsonCreate
                    );
                }
                else
                {
                    // Обновление существующего: 
                    //  - если картинка есть => передаём массив с изображением
                    //  - если картинку убрали => передаём "[]" чтобы принудительно очистить media в БД
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

                    await uow.Reviews.UpdateReviewAsync(
                        actorId: _currentUser.Id,
                        reviewId: SelectedRow.ReviewId.Value,
                        reviewerId: _currentUser.Id,
                        comment: comment,
                        rating: rating,
                        mediaJson: mediaJsonUpdate
                    );
                }

                IsEditOpen = false;
                EditImageName = "";
                EditImageBase64 = "";
                EditImagePreview = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(ReviewRow? row)
        {
            var target = row ?? SelectedRow;
            if (target?.ReviewId is null)
                return;

            if (MessageBox.Show(
                    $"Удалить отзыв для заказа №{target.OrderId}?",
                    "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                await uow.Reviews.DeleteReviewAsync(
                    actorId: _currentUser.Id,
                    reviewId: target.ReviewId.Value,
                    reviewerId: _currentUser.Id);

                if (IsEditOpen) IsEditOpen = false;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SelectImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Выберите изображение для отзыва"
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

        // ===== Вспомогательные для медиа =====
        private static (string? name, string? base64) MediaFromJson(System.Text.Json.JsonDocument? doc)
        {
            if (doc is null) return (null, null);
            try
            {
                foreach (var el in doc.RootElement.EnumerateArray())
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

    // ===== Row-VM для DataTemplate
    public sealed class ReviewRow
    {
        public int OrderId { get; }
        public string ProjectTitle { get; }
        public string OtherSideName { get; }

        public int? ReviewId { get; }            // мой review_id (null — отзыва ещё нет)
        public string? MyComment { get; }
        public int? MyRating { get; }

        public string? OppComment { get; }
        public int? OppRating { get; }

        public string MyCommentPreview => string.IsNullOrWhiteSpace(MyComment) ? "[нет]" : MyComment!;
        public string OppCommentPreview => string.IsNullOrWhiteSpace(OppComment) ? "[нет]" : OppComment!;
        public string EditButtonText => ReviewId is null ? "Добавить" : "Изменить";
        public bool CanDelete => ReviewId is not null;

        // медиа моего отзыва
        public string? MyImageName { get; }
        public string? MyImageBase64 { get; }
        public ImageSource? MyImageSource { get; }
        public bool HasMyImage => MyImageSource is not null;

        // медиа оппонента (маленькое превью в списке)
        public string? OppImageName { get; }
        public ImageSource? OppImageSource { get; }
        public bool HasOppImage => OppImageSource is not null;

        public ReviewRow(
            int orderId,
            string projectTitle,
            string otherSideName,
            int? reviewId,
            string? myComment,
            int? myRating,
            string? oppComment,
            int? oppRating,
            string? myImageName,
            string? myImageBase64,
            ImageSource? myImageSource,
            string? oppImageName,
            ImageSource? oppImageSource)
        {
            OrderId = orderId;
            ProjectTitle = projectTitle;
            OtherSideName = otherSideName;
            ReviewId = reviewId;
            MyComment = myComment;
            MyRating = myRating;
            OppComment = oppComment;
            OppRating = oppRating;
            MyImageName = myImageName;
            MyImageBase64 = myImageBase64;
            MyImageSource = myImageSource;
            OppImageName = oppImageName;
            OppImageSource = oppImageSource;
        }
    }
}
