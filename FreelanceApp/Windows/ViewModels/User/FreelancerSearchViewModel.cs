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

        [ObservableProperty] private int inviteeId;

        public FreelancerSearchViewModel(User currentUser) => _currentUser = currentUser;

        public async Task InitializeAsync()
        {
            await SearchAsync();
        }

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
                var msg = (Application.Current.TryFindResource("FreelancerSearch_Error_Search") as string
                           ?? "Ошибка поиска:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                {
                    var text = Application.Current.TryFindResource("FreelancerSearch_Info_NoProjects") as string
                               ?? "Нет доступных проектов для приглашения.";
                    var caption = Application.Current.TryFindResource("Common_Info") as string ?? "Информация";
                    MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("FreelancerSearch_Error_LoadProjects") as string
                           ?? "Ошибка загрузки проектов:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                var text = Application.Current.TryFindResource("FreelancerSearch_Info_InviteSent") as string
                           ?? "Приглашение отправлено";
                var caption = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);

                IsProjectPickerVisible = false;
                OpenProjects.Clear();
                SelectedProject = null;
                InviteeId = 0;
                await SearchAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("FreelancerSearch_Error_SendInvite") as string
                           ?? "Ошибка отправки приглашения:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
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
                var msg = (Application.Current.TryFindResource("FreelancerSearch_Error_ClosePicker") as string
                           ?? "Ошибка закрытия окна:") + " " +
                          (ex.InnerException?.Message ?? ex.Message);
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string
                              ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
