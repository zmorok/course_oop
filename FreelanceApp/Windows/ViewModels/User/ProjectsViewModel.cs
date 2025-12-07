using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using FreelanceApp.Helpers;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class ProjectsViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private ObservableCollection<ProjectItemViewModel> projects = [];
        [ObservableProperty] private ProjectItemViewModel? selectedProject;
        [ObservableProperty] private bool showOnlyMine;

        [ObservableProperty] private string statusFilter = ""; // "", "draft", "open", "in_progress"
        partial void OnStatusFilterChanged(string value) { _ = LoadAsync(); }


        public string ToggleMineButtonText
        {
            get
            {
                var key = ShowOnlyMine ? "Projects_Toggle_ShowAll" : "Projects_Toggle_ShowMine";
                var localized = Application.Current.TryFindResource(key) as string;
                if (localized is not null)
                    return localized;

                // Fallback (русский текст) на случай отсутствия ресурса
                return ShowOnlyMine ? "Показать все" : "Показать только мои";
            }
        }

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private string? title;
        [ObservableProperty] private string? description;
        [ObservableProperty] private string? selectedStatus; // "draft" | "open" | "in_progress"
        [ObservableProperty] private string? mediaText;      // JSON-строка (для совместимости, не редактируется пользователем)
        [ObservableProperty] private string? formImageName;
        [ObservableProperty] private string? formImageBase64;
        [ObservableProperty] private ImageSource? formImagePreview;

        public ProjectsViewModel(User currentUser)
        {
            _currentUser = currentUser;
        }

        public async Task InitializeAsync()
        {
            await LoadAsync();
        }

        
        private async Task LoadAsync()
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                Projects.Clear();

                var myOrdersAsCustomer = await uow.Orders.GetOrdersByCustomerAsync(_currentUser.Id, limit: 100);
                var respondedProjectIds = myOrdersAsCustomer.Select(o => o.ProjectId).ToHashSet();

                if (ShowOnlyMine)
                {

                    var list = string.IsNullOrEmpty(StatusFilter)
                        ? await uow.Projects.GetProjectsByCustomerAsync(_currentUser.Id, limit: 100)
                        : await uow.Projects.GetProjectsByCustomerAndStatusAsync(_currentUser.Id, StatusFilter, limit: 100);

                    foreach (var p in list)
                        {
                            Projects.Add(new ProjectItemViewModel(
                                p,
                                status: p.Status ?? "",
                                isMine: true,
                                showRespondButton: false
                            ));
                        }
                }
                else
                {
                    if (!string.IsNullOrEmpty(StatusFilter))
                    {
                        var list = await uow.Projects.GetProjectsWithoutStatusAsync(StatusFilter, limit: 100);
                        foreach (var p in list)
                        {
                            bool isMine = p.Id_Customer == _currentUser.Id;
                            bool canRespond = !isMine && !respondedProjectIds.Contains(p.Id_Project);
                            Projects.Add(ProjectItemViewModel.FromProjectWithoutStatus(p, StatusFilter, isMine, canRespond));
                        }
                    }
                    else
                    {
                        var list = await uow.Projects.GetAllProjectsAsync(limit: 100);
                        foreach (var p in list)
                        {
                            bool isMine = p.CustomerId == _currentUser.Id;
                            bool canRespond = !isMine && !respondedProjectIds.Contains(p.Id);
                            Projects.Add(new ProjectItemViewModel(
                                p,
                                status: p.Status ?? "",
                                isMine: isMine,
                                showRespondButton: canRespond
                            ));
                        }
                    }
                }

                OnPropertyChanged(nameof(ToggleMineButtonText));
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Projects_Error_Load") as string
                           ?? "Ошибка при загрузке проектов:") + "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Projects_Error_LoadCaption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task ToggleMineAsync()
        {
            ShowOnlyMine = !ShowOnlyMine;
            OnPropertyChanged(nameof(ToggleMineButtonText));
            CloseForm();
            await LoadAsync();
        }

        [RelayCommand]
        private void Add()
        {
            SelectedProject = null;
            OpenFormFor(null);
        }

        [RelayCommand]
        private void Edit()
        {
            if (SelectedProject is null)
            {
                var text = Application.Current.TryFindResource("Projects_Warn_SelectForEdit") as string
                           ?? "Выберите проект для редактирования.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!SelectedProject.IsMine)
            {
                var text = Application.Current.TryFindResource("Projects_Warn_EditNotMine") as string
                           ?? "Редактировать можно только свои проекты.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            OpenFormFor(SelectedProject.Project);
        }

        [RelayCommand]
        private async Task DeleteAsync()
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            if (SelectedProject?.Project is null)
            {
                var text = Application.Current.TryFindResource("Projects_Warn_SelectForDelete") as string
                           ?? "Выберите проект.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmTemplate = Application.Current.TryFindResource("Projects_Confirm_Delete") as string
                                  ?? "Удалить проект №{0}?";
            var confirmCaption = Application.Current.TryFindResource("Projects_Confirm_Caption") as string
                                 ?? "Подтверждение";
            var confirmText = string.Format(confirmTemplate, SelectedProject.Project.Title);

            var confirm = MessageBox.Show(
                confirmText,
                confirmCaption,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await uow.Projects.DeleteProjectAsync(_currentUser.Id, SelectedProject.Project.Id);
                var textDeleted = Application.Current.TryFindResource("Projects_Info_Deleted") as string
                                  ?? "Проект удалён.";
                var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(textDeleted, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Projects_Error_Delete") as string
                           ?? "Ошибка при удалении проекта:") + "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Projects_Error_LoadCaption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel() => CloseForm();

        [RelayCommand]
        private async Task SaveAsync()
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            // простая валидация
            var title = (Title ?? "").Trim();
            var description = (Description ?? "").Trim();
            var status = string.IsNullOrWhiteSpace(SelectedStatus) ? "draft" : SelectedStatus!;
            var imageBase64 = (FormImageBase64 ?? "").Trim();

            // строим JSON для медиа, если выбрали файл
            string mediaJson = "[]";
            if (!string.IsNullOrWhiteSpace(imageBase64))
            {
                var media = new[]
                {
                    new
                    {
                        type = "image",
                        name = FormImageName ?? "image",
                        content = imageBase64
                    }
                };
                mediaJson = JsonSerializer.Serialize(media);
            }

            try
            {
                if (SelectedProject?.Project is null)
                {
                    // создать
                    await uow.Projects.CreateProjectAsync(
                        actorId: _currentUser.Id,
                        userId: _currentUser.Id,
                        title: title,
                        status: status,
                        description: description,
                        mediaJson: string.IsNullOrWhiteSpace(mediaJson) ? "[]" : mediaJson
                    );
                    var textCreated = Application.Current.TryFindResource("Projects_Info_Created") as string
                                      ?? "Проект создан.";
                    var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                    MessageBox.Show(textCreated, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // обновить
                    await uow.Projects.UpdateProjectAsync(
                        actorId: _currentUser.Id,
                        projectId: SelectedProject.Project.Id,
                        title: title,
                        status: status,
                        description: description,
                        mediaJson: string.IsNullOrWhiteSpace(mediaJson) ? "[]" : mediaJson
                    );
                    var textUpdated = Application.Current.TryFindResource("Projects_Info_Updated") as string
                                      ?? "Проект обновлён.";
                    var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                    MessageBox.Show(textUpdated, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Projects_Error_Save") as string
                           ?? "Ошибка при сохранении проекта:") + "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Projects_Error_LoadCaption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RespondAsync(ProjectItemViewModel? item)
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            if (item?.Project is null) return;
            if (item.IsMine)
            {
                var text = Application.Current.TryFindResource("Projects_Warn_SelfResponse") as string
                           ?? "Нельзя откликаться на собственный проект.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Внимание";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                await uow.Orders.CreateOrderAsync(
                    actorId: _currentUser.Id,
                    projectId: item.Project.Id,
                    freelancerId: _currentUser.Id,
                    status: "pending",
                    deadline: null
                );
                var textResponded = Application.Current.TryFindResource("Projects_Info_Responded") as string
                                    ?? "Вы откликнулись на проект!";
                var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(textResponded, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Projects_Error_Respond") as string
                           ?? "Ошибка при отклике:") + " " + ex.Message;
                var caption = Application.Current.TryFindResource("Projects_Error_LoadCaption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SelectForEdit(ProjectItemViewModel? item)
        {
            if (item is null)
            {
                CloseForm();
                return;
            }

            SelectedProject = item;
            if (item.IsMine)
            {
                OpenFormFor(item.Project);
            }
            else
            {
                CloseForm();
            }
        }

        [RelayCommand]
        private async Task StatusFilterChangedAsync(string? newFilter)
        {
            StatusFilter = newFilter ?? "";
            await LoadAsync();
        }

        // ===== Вспомогательные

        private void OpenFormFor(Project? p)
        {
            if (p is null)
            {
                Title = "";
                Description = "";
                MediaText = "";
                SelectedStatus = "draft";
                FormImageName = "";
                FormImageBase64 = "";
                FormImagePreview = null;
            }
            else
            {
                Title = p.Title;
                Description = p.Description;
                MediaText = p.Media is null ? "" : p.Media.RootElement.GetRawText();
                SelectedStatus = p.Status ?? "draft";

                // извлекаем первую картинку, если есть
                (FormImageName, FormImageBase64) = MediaJsonHelper.ExtractFirstImage(p.Media);
                FormImagePreview = MediaJsonHelper.CreateImageSource(FormImageBase64);
            }
            IsFormOpen = true;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            SelectedProject = null;
        }

        [RelayCommand]
        private void ClearSelection()
        {
            CloseForm();
        }

        [RelayCommand]
        private void SelectImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Выберите изображение проекта"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dialog.FileName);
                    FormImageBase64 = Convert.ToBase64String(bytes);
                    FormImageName = Path.GetFileName(dialog.FileName);
                    FormImagePreview = MediaJsonHelper.CreateImageSource(FormImageBase64);
                }
                catch (Exception ex)
                {
                    var msg = (Application.Current.TryFindResource("Projects_Error_ReadFile") as string
                               ?? "Не удалось прочитать файл:") + " " + ex.Message;
                    var caption = Application.Current.TryFindResource("Projects_Error_LoadCaption") as string
                                  ?? "Ошибка";
                    MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ClearImage()
        {
            FormImageBase64 = "";
            FormImageName = "";
            FormImagePreview = null;
        }
    }

    public sealed class ProjectItemViewModel
    {
        public Project Project { get; }
        public string Status { get; }
        public bool IsMine { get; }
        public bool ShowRespondButton { get; }

        public string StatusDisplay
        {
            get
            {
                string? key = Status switch
                {
                    "draft" => "Projects_Status_Draft",
                    "open" => "Projects_Status_Open",
                    "in_progress" => "Projects_Status_InProgress",
                    "completed" => "Projects_Status_Completed",
                    _ => null
                };

                if (!string.IsNullOrEmpty(key))
                {
                    var localized = Application.Current.TryFindResource(key) as string;
                    if (localized is not null)
                        return localized;
                }

                // Fallback (русский текст) на случай отсутствия ресурса
                return Status switch
                {
                    "draft" => "Черновик",
                    "open" => "Открыт",
                    "in_progress" => "В прогрессе",
                    "completed" => "Завершён",
                    _ => Status
                };
            }
        }

        public ProjectItemViewModel(Project project, string status, bool isMine, bool showRespondButton)
        {
            Project = project;
            Status = status;
            IsMine = isMine;
            ShowRespondButton = showRespondButton;
        }

        // конструктор «проекции» для v_projects (ProjectWithoutStatus)
        public static ProjectItemViewModel FromProjectWithoutStatus(
            DAL.Models.Views.ProjectWithoutStatus pws,
            string status,
            bool isMine,
            bool showRespondButton)
        {
            var p = new Project
            {
                Id = pws.Id_Project,
                CustomerId = pws.Id_Customer,
                Title = pws.Title,
                Description = pws.Description,
                Media = pws.Media,
                Status = status
            };
            return new ProjectItemViewModel(p, status, isMine, showRespondButton);
        }
    }
}
