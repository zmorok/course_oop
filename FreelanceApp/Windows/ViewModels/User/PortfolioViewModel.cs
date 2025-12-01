using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class PortfolioViewModel : ObservableObject
    {
        private readonly User _currentUser;

        // Список карточек портфолио
        [ObservableProperty] private ObservableCollection<PortfolioItemViewModel> portfolios = [];

        // Выбранная карточка (для Delete/редактирования)
        [ObservableProperty] private PortfolioItemViewModel? selectedPortfolio;

        // Панель формы
        [ObservableProperty] private bool isFormOpen;

        // Поля формы
        [ObservableProperty] private string? formDescription;
        [ObservableProperty] private string? formMediaJson;
        [ObservableProperty] private string? formSkills;     // через запятую
        [ObservableProperty] private string? formExperience;

        public PortfolioViewModel(User currentUser)
        {
            _currentUser = currentUser;
        }

        public async Task InitializeAsync() => await LoadAsync();

        // === Загрузка ===
        private async Task LoadAsync()
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                Portfolios.Clear();

                var list = await uow.Portfolios.GetPortfoliosAsync(_currentUser.Id);
                foreach (var p in list)
                {
                    Portfolios.Add(new PortfolioItemViewModel(p));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при загрузке портфолио:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // === Команды панели ===

        [RelayCommand]
        private void Add()
        {
            SelectedPortfolio = null;
            OpenFormFor(null);
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseForm();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));

            var description = (FormDescription ?? "").Trim();
            var mediaJson = (FormMediaJson ?? "").Trim();
            var skillsCsv = (FormSkills ?? "").Trim();
            var experience = (FormExperience ?? "").Trim();

            // валидация JSON (если что-то введено)
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

            var skills = skillsCsv.Length == 0
                ? Array.Empty<string>()
                : skillsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            try
            {
                if (SelectedPortfolio is null)
                {
                    // создать
                    await uow.Portfolios.CreatePortfolioAsync(
                        actorId: _currentUser.Id,
                        userId: _currentUser.Id,
                        description: description,
                        mediaJson: string.IsNullOrWhiteSpace(mediaJson) ? "[]" : mediaJson,
                        skills: skills,
                        experience: experience
                    );

                    MessageBox.Show("Портфолио добавлено.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // обновить
                    await uow.Portfolios.UpdatePortfolioAsync(
                        actorId: _currentUser.Id,
                        userId: _currentUser.Id,
                        portfolioId: SelectedPortfolio.Id,
                        description: description,
                        mediaJson: string.IsNullOrWhiteSpace(mediaJson) ? "[]" : mediaJson,
                        skills: skills,
                        experience: experience
                    );

                    MessageBox.Show("Портфолио обновлено.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при сохранении портфолио:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedPortfolio is null)
            {
                MessageBox.Show("Выберите портфолио для удаления.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await DeleteItemAsync(SelectedPortfolio);
        }

        // Удаление прямо с карточки (через CommandParameter)
        [RelayCommand]
        private async Task DeleteItemAsync(PortfolioItemViewModel? item)
        {
            if (item is null) return;

            var confirm = MessageBox.Show(
                $"Удалить портфолио №{item.Id}?\n{item.Description}",
                "Подтвердите удаление",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            await using var uow = new UnitOfWork(DbContextFactory.CreateDbContext(_currentUser));
            try
            {
                await uow.Portfolios.DeletePortfolioAsync(
                    actorId: _currentUser.Id,
                    userId: _currentUser.Id,
                    portfolioId: item.Id);

                MessageBox.Show("Портфолио удалено.", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                if (ReferenceEquals(SelectedPortfolio, item))
                    CloseForm();

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при удалении портфолио:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Выбор карточки для редактирования (с кнопки «Редактировать» на карточке)
        [RelayCommand]
        private void SelectForEdit(PortfolioItemViewModel? item)
        {
            if (item is null) return;
            SelectedPortfolio = item;
            OpenFormFor(item.Model);
        }

        // === Вспомогательные ===
        private void OpenFormFor(Portfolio? p)
        {
            if (p is null)
            {
                FormDescription = "";
                FormMediaJson = "";
                FormSkills = "";
                FormExperience = "";
            }
            else
            {
                FormDescription = p.Description ?? "";
                FormMediaJson = p.Media is null ? "" : p.Media.RootElement.GetRawText();
                FormSkills = p.Skills is null ? "" : string.Join(", ", p.Skills);
                FormExperience = p.Experience ?? "";
            }
            IsFormOpen = true;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            SelectedPortfolio = null;
        }
    }

    // VM одной карточки
    public sealed class PortfolioItemViewModel
    {
        public Portfolio Model { get; }
        public int Id => Model.Id;
        public string Description => Model.Description ?? "";
        public string Experience => Model.Experience ?? "";
        public string Media => TryMedia(Model.Media);
        public string Skills => Model.Skills is null ? "" : string.Join(", ", Model.Skills);

        public PortfolioItemViewModel(Portfolio model) => Model = model;

        private static string TryMedia(JsonDocument? doc)
        {
            if (doc is null) return "";
            try { return doc.RootElement.ToString(); }
            catch { return ""; }
        }
    }
}
