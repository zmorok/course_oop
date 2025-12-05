using System;
using System.Collections.Generic;
using System.Windows;

namespace FreelanceApp.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private static readonly Uri LightThemeUri = new("/Styles/Brushes/Brushes_WHITE.xaml", UriKind.Relative);
        private static readonly Uri DarkThemeUri = new("/Styles/Brushes/Brushes_BLACK.xaml", UriKind.Relative);

        static ThemeManager()
        {
            CurrentTheme = DetectCurrentTheme();
        }

        public static AppTheme CurrentTheme { get; private set; }

        public static void Apply(AppTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            var targetUri = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;
            var (owner, index) = FindBrushDictionary(app.Resources.MergedDictionaries);
            if (owner != null)
            {
                if (index >= 0 && index < owner.Count)
                {
                    var existing = owner[index];
                    if (SameSource(existing.Source, targetUri))
                    {
                        CurrentTheme = theme;
                        return;
                    }

                    // меняем источник на существующем словаре, чтобы динамические ресурсы сразу обновились
                    existing.Source = targetUri;
                }
                else
                {
                    owner.Insert(0, new ResourceDictionary { Source = targetUri });
                }
            }
            else
            {
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = targetUri });
            }

            CurrentTheme = theme;
        }

        private static AppTheme DetectCurrentTheme()
        {
            var app = Application.Current;
            if (app == null) return AppTheme.Light;

            var (owner, index) = FindBrushDictionary(app.Resources.MergedDictionaries);
            var source = index >= 0 && owner != null ? owner[index].Source?.OriginalString : string.Empty;

            return !string.IsNullOrWhiteSpace(source) && source.Contains("BLACK", StringComparison.OrdinalIgnoreCase)
                ? AppTheme.Dark
                : AppTheme.Light;
        }

        private static (IList<ResourceDictionary>? owner, int index) FindBrushDictionary(IList<ResourceDictionary> dictionaries)
        {
            foreach (var dictionary in dictionaries)
            {
                if (dictionary.Source != null && dictionary.Source.OriginalString.EndsWith("/Styles/Styles.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    var nestedResult = FindBrushDictionary(dictionary.MergedDictionaries);
                    if (nestedResult.owner != null) return nestedResult;
                }
            }

            for (var i = 0; i < dictionaries.Count; i++)
            {
                if (IsBrushDictionary(dictionaries[i].Source))
                {
                    return (dictionaries, i);
                }
            }

            return (null, -1);
        }

        private static bool IsBrushDictionary(Uri? source)
        {
            if (source == null) return false;

            var path = source.OriginalString;
            return path.Contains("Brushes_WHITE.xaml", StringComparison.OrdinalIgnoreCase)
                   || path.Contains("Brushes_BLACK.xaml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameSource(Uri? current, Uri target) =>
            current != null && string.Equals(current.OriginalString, target.OriginalString, StringComparison.OrdinalIgnoreCase);
    }
}
