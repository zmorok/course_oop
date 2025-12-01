using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Windows;

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

        public ReviewsViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync() => await LoadAsync();

        private async Task LoadAsync()
        {
            try
            {
                Rows.Clear();
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                // получаем строки «заказы из архива + мои/их отзывы»
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

                    Rows.Add(new ReviewRow(
                        orderId: r.Order_Id,
                        projectTitle: r.Project_Title,
                        otherSideName: otherName,
                        reviewId: myReviewId,
                        myComment: myComment,
                        myRating: myRating,
                        oppComment: oppComment,
                        oppRating: oppRating
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

            IsEditOpen = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            IsEditOpen = false;
            EditComment = "";
            EditRating = 5;
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

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

                if (SelectedRow.ReviewId is null)
                {
                    await uow.Reviews.CreateReviewAsync(
                        actorId: _currentUser.Id,
                        orderId: SelectedRow.OrderId,
                        reviewerId: _currentUser.Id,
                        comment: comment,
                        rating: rating
                    );
                }
                else
                {
                    await uow.Reviews.UpdateReviewAsync(
                        actorId: _currentUser.Id,
                        reviewId: SelectedRow.ReviewId.Value,
                        reviewerId: _currentUser.Id,
                        comment: comment,
                        rating: rating
                    );
                }

                IsEditOpen = false;
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

        public ReviewRow(
            int orderId,
            string projectTitle,
            string otherSideName,
            int? reviewId,
            string? myComment,
            int? myRating,
            string? oppComment,
            int? oppRating)
        {
            OrderId = orderId;
            ProjectTitle = projectTitle;
            OtherSideName = otherSideName;
            ReviewId = reviewId;
            MyComment = myComment;
            MyRating = myRating;
            OppComment = oppComment;
            OppRating = oppRating;
        }
    }
}
