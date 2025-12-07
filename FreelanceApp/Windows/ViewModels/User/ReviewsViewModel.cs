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
using FreelanceApp.Helpers;

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
                        ? (r.Freelancer_Fullname ??
                           (Application.Current.TryFindResource("Reviews_Text_DeletedUser") as string ?? "[удалён]"))
                        : r.Customer_Fullname;

                    var myComment = iAmCustomer ? r.Customer_Comment : r.Freelancer_Comment;
                    var myRating = iAmCustomer ? r.Customer_Rating : r.Freelancer_Rating;
                    var myReviewId = iAmCustomer ? r.Customer_Review_Id : r.Freelancer_Review_Id;

                    var oppComment = iAmCustomer ? r.Freelancer_Comment : r.Customer_Comment;
                    var oppRating = iAmCustomer ? r.Freelancer_Rating : r.Customer_Rating;

                    // медиа берём напрямую из v_orders_reviews (customer_media / freelancer_media)
                    var myMediaDoc = iAmCustomer ? r.Customer_Media : r.Freelancer_Media;
                    var oppMediaDoc = iAmCustomer ? r.Freelancer_Media : r.Customer_Media;

                    var (myImageName, myImageBase64) = MediaJsonHelper.ExtractFirstImage(myMediaDoc);
                    var (oppImageName, _) = MediaJsonHelper.ExtractFirstImage(oppMediaDoc);

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
                        myMedia: myMediaDoc,
                        oppImageName: oppImageName,
                        oppMedia: oppMediaDoc
                    ));
                }

                // закрыть форму, сбросить выбор
                IsEditOpen = false;
                SelectedRow = null;
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Reviews_Error_Load") as string
                           ?? "Ошибка загрузки отзывов:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Reviews_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // открыть форму для добавления/редактирования
        [RelayCommand]
        private void OpenEditFor(ReviewRow? row)
        {
            if (row is null) return;
            SelectedRow = row;

            var titleKey = row.ReviewId is null ? "Reviews_Edit_Title_New" : "Reviews_Edit_Title_Edit";
            var titleLocalized = Application.Current.TryFindResource(titleKey) as string;
            PanelTitle = titleLocalized ?? (row.ReviewId is null ? "Новый отзыв" : "Изменить отзыв");
            EditComment = row.MyComment ?? "";
            EditRating = (row.MyRating is >= 1 and <= 5) ? row.MyRating.Value : 5;
            EditImageName = row.MyImageName;
            EditImageBase64 = row.MyImageBase64;
            EditImagePreview = MediaJsonHelper.CreateImageSource(EditImageBase64);

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
                var text = Application.Current.TryFindResource("Reviews_Warn_EmptyComment") as string
                           ?? "Комментарий не может быть пустым.";
                var caption = Application.Current.TryFindResource("Reviews_Warn_Caption") as string
                              ?? "Проверьте данные";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var msg = (Application.Current.TryFindResource("Reviews_Error_Save") as string
                           ?? "Ошибка сохранения:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Reviews_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteAsync(ReviewRow? row)
        {
            var target = row ?? SelectedRow;
            if (target?.ReviewId is null)
                return;

            var confirmTemplate = Application.Current.TryFindResource("Reviews_Confirm_Delete") as string
                                  ?? "Удалить отзыв для заказа №{0}?";
            var confirmCaption = Application.Current.TryFindResource("Reviews_Confirm_Caption") as string
                                 ?? "Подтверждение";
            var confirmText = string.Format(confirmTemplate, target.OrderId);

            if (MessageBox.Show(
                    confirmText,
                    confirmCaption,
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
                var msg = (Application.Current.TryFindResource("Reviews_Error_Delete") as string
                           ?? "Ошибка удаления:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Reviews_Error_Load_Caption") as string
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
                Title = "Выберите изображение для отзыва"
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
                    var msg = (Application.Current.TryFindResource("Reviews_Error_ReadFile") as string
                               ?? "Не удалось прочитать файл:") + " " + ex.Message;
                    var caption = Application.Current.TryFindResource("Reviews_Error_Load_Caption") as string
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

        // Вспомогательные методы для медиа вынесены в Helpers.MediaJsonHelper.
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

        public string MyCommentPreview
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(MyComment))
                    return MyComment!;

                return Application.Current.TryFindResource("Reviews_Text_None") as string ?? "[нет]";
            }
        }

        public string OppCommentPreview
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(OppComment))
                    return OppComment!;

                return Application.Current.TryFindResource("Reviews_Text_None") as string ?? "[нет]";
            }
        }

        public string EditButtonText
        {
            get
            {
                var key = ReviewId is null ? "Reviews_Button_Add" : "Reviews_Button_Edit";
                var localized = Application.Current.TryFindResource(key) as string;
                if (localized is not null)
                    return localized;

                return ReviewId is null ? "Добавить" : "Изменить";
            }
        }
        public bool CanDelete => ReviewId is not null;

        // медиа моего отзыва
        public string? MyImageName { get; }
        public string? MyImageBase64 { get; }
        public JsonDocument? MyMedia { get; }

        // медиа оппонента (маленькое превью в списке)
        public string? OppImageName { get; }
        public JsonDocument? OppMedia { get; }

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
            JsonDocument? myMedia,
            string? oppImageName,
            JsonDocument? oppMedia)
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
            OppImageName = oppImageName;
            MyMedia = myMedia;
            OppMedia = oppMedia;
        }
    }
}
