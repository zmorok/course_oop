using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
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
        partial void OnStatusFilterChanged(string value) { _ = LoadAsync();}


        public string ToggleMineButtonText => ShowOnlyMine ? "Показать все" : "Показать только мои";

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
                MessageBox.Show(
                    $"Ошибка при загрузке проектов:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
                MessageBox.Show("Выберите проект для редактирования.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!SelectedProject.IsMine)
            {
                MessageBox.Show("Редактировать можно только свои проекты.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("Выберите проект.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Удалить проект №{SelectedProject.Project.Title}?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await uow.Projects.DeleteProjectAsync(_currentUser.Id, SelectedProject.Project.Id);
                MessageBox.Show("Проект удалён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении проекта:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Проект создан.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("Проект обновлён.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении проекта:\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RespondAsync(ProjectItemViewModel? item)
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            if (item?.Project is null) return;
            if (item.IsMine)
            {
                MessageBox.Show("Нельзя откликаться на собственный проект.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show("Вы откликнулись на проект!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отклике: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                (FormImageName, FormImageBase64) = ProjectItemViewModel.ExtractFirstImage(p.Media);
                FormImagePreview = ProjectItemViewModel.CreateImageSource(FormImageBase64);
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
                    FormImagePreview = ProjectItemViewModel.CreateImageSource(FormImageBase64);
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
        public bool HasImage => ImageSource is not null;
        public ImageSource? ImageSource { get; }

        public string StatusDisplay =>
            Status switch
            {
                "draft" => "Черновик",
                "open" => "Открыт",
                "in_progress" => "В прогрессе",
                "completed" => "Завершён",
                _ => Status
            };

        public ProjectItemViewModel(Project project, string status, bool isMine, bool showRespondButton)
        {
            Project = project;
            Status = status;
            IsMine = isMine;
            ShowRespondButton = showRespondButton;
            ImageSource = CreateImage(project.Media);
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

        internal static ImageSource? CreateImage(JsonDocument? doc)
        {
            if (doc is null) return null;
            try
            {
                var (name, content) = ExtractFirstImage(doc);
                if (string.IsNullOrWhiteSpace(content)) return null;

                return CreateImageSource(content);
            }
            catch
            {
                return null;
            }
        }

        internal static ImageSource? CreateImageSource(string? base64)
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

        public static (string? name, string? base64) ExtractFirstImage(JsonDocument? doc)
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
    }
}
