using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DAL;
using DAL.Models.Tables;
using FreelanceApp.Services;
using FreelanceApp.Helpers;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace FreelanceApp.Windows.ViewModels
{
    public sealed partial class PortfolioViewModel : ObservableObject
    {
        private readonly User _currentUser;

        [ObservableProperty] private ObservableCollection<PortfolioItemViewModel> portfolios = [];
        [ObservableProperty] private PortfolioItemViewModel? selectedPortfolio;

        [ObservableProperty] private bool isFormOpen;

        [ObservableProperty] private string? formDescription;
        [ObservableProperty] private string? formSkills;
        [ObservableProperty] private string? formExperience;
        [ObservableProperty] private string? formImageName;
        [ObservableProperty] private string? formImageBase64;
        [ObservableProperty] private ImageSource? formImagePreview;

        public PortfolioViewModel(User currentUser)
        {
            _currentUser = currentUser;
        }

        public async Task InitializeAsync() => await LoadAsync();

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
                var msg = (Application.Current.TryFindResource("Portfolio_Error_Load") as string ?? "Ошибка при загрузке портфолио:") + "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

            // число + суффикс лет/года/год/мес и т.п., допускается комбинация "1 год 6 месяцев"
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
                var text = Application.Current.TryFindResource("Portfolio_Warn_Experience") as string
                           ?? "Поле «Опыт» должно содержать число с указанием единицы (например, \"3 года\", \"6 мес\" или \"1 год 6 месяцев\").";
                var caption = Application.Current.TryFindResource("Portfolio_Warn_Experience_Caption") as string
                              ?? "Проверьте данные";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    var textAdded = Application.Current.TryFindResource("Portfolio_Info_Added") as string
                                    ?? "Портфолио добавлено.";
                    var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                    MessageBox.Show(textAdded, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
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

                    var textUpdated = Application.Current.TryFindResource("Portfolio_Info_Updated") as string
                                      ?? "Портфолио обновлено.";
                    var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                    MessageBox.Show(textUpdated, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                }

                CloseForm();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Portfolio_Error_Save") as string ?? "Ошибка при сохранении портфолио:") +
                          "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedPortfolio is null)
            {
                var text = Application.Current.TryFindResource("Portfolio_Error_SelectForDelete") as string
                           ?? "Выберите портфолио для удаления.";
                var caption = Application.Current.TryFindResource("Common_Warning") as string ?? "Предупреждение";
                MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await DeleteItemAsync(SelectedPortfolio);
        }

        [RelayCommand]
        private async Task DeleteItemAsync(PortfolioItemViewModel? item)
        {
            if (item is null) return;

            var tmpl = Application.Current.TryFindResource("Portfolio_Confirm_Delete") as string
                       ?? "Удалить портфолио №{0}?\n{1}";
            var captionConfirm = Application.Current.TryFindResource("Portfolio_Confirm_Caption") as string
                                 ?? "Подтвердите удаление";
            var confirmText = string.Format(tmpl, item.Id, item.Description);

            var confirm = MessageBox.Show(
                confirmText,
                captionConfirm,
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

                var textDeleted = Application.Current.TryFindResource("Portfolio_Info_Deleted") as string
                                  ?? "Портфолио удалено.";
                var captionSuccess = Application.Current.TryFindResource("Common_Success") as string ?? "Успех";
                MessageBox.Show(textDeleted, captionSuccess, MessageBoxButton.OK, MessageBoxImage.Information);

                if (ReferenceEquals(SelectedPortfolio, item))
                    CloseForm();

                await LoadAsync();
            }
            catch (Exception ex)
            {
                var msg = (Application.Current.TryFindResource("Portfolio_Error_Delete") as string ?? "Ошибка при удалении портфолио:") +
                          "\n" + ex.Message;
                var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
                MessageBox.Show(msg, caption, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void SelectForEdit(PortfolioItemViewModel? item)
        {
            if (item is null) return;
            SelectedPortfolio = item;
            OpenFormFor(item.Model);
        }

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

                (FormImageName, FormImageBase64) = MediaJsonHelper.ExtractFirstImage(p.Media);
                FormImagePreview = MediaJsonHelper.CreateImageSource(FormImageBase64);
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
                    FormImagePreview = MediaJsonHelper.CreateImageSource(FormImageBase64);
                }
                catch (Exception ex)
                {
                    var msg = (Application.Current.TryFindResource("Portfolio_Error_ReadFile") as string ?? "Не удалось прочитать файл:") +
                              $" {ex.Message}";
                    var caption = Application.Current.TryFindResource("Orders_Error_Load_Caption") as string ?? "Ошибка";
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

    public sealed class PortfolioItemViewModel
    {
        public Portfolio Model { get; }
        public int Id => Model.Id;
        public string Description => Model.Description ?? "";
        public string Experience => Model.Experience ?? "";
        public string Skills => Model.Skills is null ? "" : string.Join(", ", Model.Skills);
        public IReadOnlyList<string> SkillsList => Model.Skills?.ToList() ?? [];

        public PortfolioItemViewModel(Portfolio model)
        {
            Model = model;
        }
    }
}
