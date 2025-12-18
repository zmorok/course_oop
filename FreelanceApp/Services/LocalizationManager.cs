using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FreelanceApp.Services
{
    public enum AppLanguage
    {
        Ru,
        En
    }

    public static class LocalizationManager
    {
        private const string RuDictionaryPath = "/Strings/Ru/Locale_RU.xaml";
        private const string EnDictionaryPath = "/Strings/En/Locale_EN.xaml";

        private static readonly Dictionary<AppLanguage, Uri> LanguageUris = new()
        {
            [AppLanguage.Ru] = new Uri(RuDictionaryPath, UriKind.Relative),
            [AppLanguage.En] = new Uri(EnDictionaryPath, UriKind.Relative)
        };

        private const string LocaleTag = "LocaleDictionary";

        public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Ru;

        public static event EventHandler? LanguageChanged;

        public static void SetLanguage(AppLanguage language)
        {
            if (!LanguageUris.TryGetValue(language, out var uri))
                throw new ArgumentOutOfRangeException(nameof(language));

            var app = Application.Current;
            if (app is null) return;

            var dictionaries = app.Resources.MergedDictionaries;

            // Удаляем старый словарь локали (по Tag).
            var toRemove = dictionaries
                .Where(d => Equals(d[LocaleTag], true))
                .ToList();

            foreach (var d in toRemove)
                dictionaries.Remove(d);

            // Подключаем новый словарь.
            var localeDict = new ResourceDictionary
            {
                Source = uri
            };
            localeDict[LocaleTag] = true;

            dictionaries.Insert(0, localeDict); // выше остальных, чтобы перекрывать значения

            CurrentLanguage = language;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
