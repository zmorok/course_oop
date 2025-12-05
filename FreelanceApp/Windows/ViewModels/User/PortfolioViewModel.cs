using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Text.RegularExpressions;

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
        [ObservableProperty] private string? formSkills;     // через запятую
        [ObservableProperty] private string? formExperience;
        [ObservableProperty] private string? formImageName;
        [ObservableProperty] private string? formImageBase64;
        [ObservableProperty] private ImageSource? formImagePreview;

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
            var skillsCsv = (FormSkills ?? "").Trim();
            var experience = (FormExperience ?? "").Trim();
            var imageBase64 = (FormImageBase64 ?? "").Trim();

            // Валидация опыта: число + суффикс лет/года/год/мес и т.п., допускается комбинация "1 год 6 месяцев"
            var experiencePattern = @"^(?ix)
                (                                   # вариант с годами (и, возможно, месяцами)
                    \d+(\.\d+)?\s*(год|года|лет|г\.?|лет\.?)
                    ( \s+\d+(\.\d+)?\s*(месяц|месяца|месяцев|мес\.?|мес) )?
                )
                |
                (                                   # только месяцы
                    \d+(\.\d+)?\s*(месяц|месяца|месяцев|мес\.?|мес)
                )
            $";
            if (!string.IsNullOrWhiteSpace(experience) && !Regex.IsMatch(experience, experiencePattern, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace))
            {
                MessageBox.Show(
                    "Поле «Опыт» должно содержать число с указанием единицы (например, \"3 года\", \"6 мес\" или \"1 год 6 месяцев\").",
                    "Проверьте данные",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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
                        mediaJson: mediaJson,
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
                        mediaJson: mediaJson,
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
                FormSkills = "";
                FormExperience = "";
                FormImageName = "";
                FormImageBase64 = "";
                FormImagePreview = null;
            }
            else
            {
                FormDescription = p.Description ?? "";
                FormSkills = p.Skills is null ? "" : string.Join(", ", p.Skills);
                FormExperience = p.Experience ?? "";

                // извлечём первую картинку, если есть
                (FormImageName, FormImageBase64) = PortfolioItemViewModel.ExtractFirstImage(p.Media);
                FormImagePreview = PortfolioItemViewModel.CreateImageSource(FormImageBase64);
            }
            IsFormOpen = true;
        }

        private void CloseForm()
        {
            IsFormOpen = false;
            SelectedPortfolio = null;
        }

        [RelayCommand]
        private void SelectImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "Выберите изображение"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var bytes = File.ReadAllBytes(dialog.FileName);
                    FormImageBase64 = Convert.ToBase64String(bytes);
                    FormImageName = Path.GetFileName(dialog.FileName);
                    FormImagePreview = PortfolioItemViewModel.CreateImageSource(FormImageBase64);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось прочитать файл: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

    // VM одной карточки
    public sealed class PortfolioItemViewModel
    {
        public Portfolio Model { get; }
        public int Id => Model.Id;
        public string Description => Model.Description ?? "";
        public string Experience => Model.Experience ?? "";
        public string Skills => Model.Skills is null ? "" : string.Join(", ", Model.Skills);
        public IReadOnlyList<string> SkillsList => Model.Skills?.ToList() ?? [];
        public bool HasImage => ImageSource is not null;
        public ImageSource? ImageSource { get; }

        public PortfolioItemViewModel(Portfolio model)
        {
            Model = model;
            ImageSource = CreateImage(model.Media);
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
            catch { }

            return (null, null);
        }
    }
}
