using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;           // Project
using DAL.Models.Views;            // FreelancerRow
using FreelanceApp.Services;
using System.Windows;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class FreelancerSearchViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private string searchQuery = "";
        [ObservableProperty] private ObservableCollection<FreelancerRow> freelancers = [];
        [ObservableProperty] private FreelancerRow? selectedFreelancer;

        [ObservableProperty] private ObservableCollection<Project> openProjects = [];
        [ObservableProperty] private Project? selectedProject;
        [ObservableProperty] private bool isProjectPickerVisible;   // видимость нижней панели

        // хранить Id выбранного приглашённого (когда открываем панель)
        [ObservableProperty] private int inviteeId;

        public FreelancerSearchViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync()
        {
            await SearchAsync(); // начальный поиск (пустой запрос)
        }

        // Поиск
        [RelayCommand]
        private async Task SearchAsync()
        {
            try
            {
                Freelancers.Clear();
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                var rows = await uow.Search.SearchFreelancersAsync(_currentUser.Id, (SearchQuery ?? "").Trim());
                foreach (var r in rows) Freelancers.Add(r);

                // сбрасываем нижнюю панель
                OpenProjects.Clear();
                SelectedProject = null;
                IsProjectPickerVisible = false;
                InviteeId = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Открыть выбор проекта для фрилансера
        [RelayCommand]
        private async Task OpenProjectPickerAsync(FreelancerRow? row)
        {
            if (row is null) return;

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                var freeProjects = await uow.Search.GetFreeProjectsAsync(_currentUser.Id, row.Id);

                OpenProjects = new ObservableCollection<Project>(freeProjects);
                SelectedProject = OpenProjects.FirstOrDefault();
                InviteeId = row.Id;
                IsProjectPickerVisible = OpenProjects.Count > 0;

                if (OpenProjects.Count == 0)
                    MessageBox.Show("Нет доступных проектов для приглашения.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки проектов: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Отправить приглашение
        [RelayCommand]
        private async Task SendInviteAsync()
        {
            if (InviteeId == 0 || SelectedProject is null)
                return;

            try
            {
                await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
                await uow.Search.SendProjectInviteAsync(
                    actorId: _currentUser.Id,
                    inviteeId: InviteeId,
                    projectId: SelectedProject.Id);

                MessageBox.Show("Приглашение отправлено", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // очистим панель, перезагрузим список
                IsProjectPickerVisible = false;
                OpenProjects.Clear();
                SelectedProject = null;
                InviteeId = 0;
                await SearchAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки приглашения: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CloseProjectPickerAsync()
        {
            try
            {
                IsProjectPickerVisible = false;
                OpenProjects.Clear();
                SelectedProject = null;
                InviteeId = 0;
                await SearchAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка закрытия окна: {ex.InnerException?.Message ?? ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
