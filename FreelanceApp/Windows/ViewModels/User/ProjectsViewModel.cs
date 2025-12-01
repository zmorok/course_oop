using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

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
        [ObservableProperty] private string? mediaText;      // JSON-строка

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
                $"Удалить проект №{SelectedProject.Project.Id}?",
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
            var mediaJson = (MediaText ?? "").Trim();

            // если JSON указан — проверим валидность, чтобы не падать на БД слое
            if (!string.IsNullOrWhiteSpace(mediaJson))
            {
                try { _ = JsonDocument.Parse(mediaJson); }
                catch
                {
                    MessageBox.Show("Поле «Медиа (JSON)» содержит некорректный JSON.",
                        "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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
            if (item is null) return;
            SelectedProject = item;
            if (item.IsMine) OpenFormFor(item.Project);
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
            }
            else
            {
                Title = p.Title;
                Description = p.Description;
                MediaText = p.Media is null ? "" : p.Media.RootElement.GetRawText();
                SelectedStatus = p.Status ?? "draft";
            }
            IsFormOpen = true;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            SelectedProject = null;
        }
    }

    public sealed class ProjectItemViewModel
    {
        public Project Project { get; }
        public string Status { get; }
        public bool IsMine { get; }
        public bool ShowRespondButton { get; }

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
