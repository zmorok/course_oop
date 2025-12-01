using System.Windows;
using System.Windows.Controls;

namespace FreelanceApp.Services
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached(
                "UpdatingPassword",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject d) =>
            (string)d.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject d, string value) =>
            d.SetValue(BoundPasswordProperty, value);

        public static bool GetBindPassword(DependencyObject d) =>
            (bool)d.GetValue(BindPasswordProperty);
        public static void SetBindPassword(DependencyObject d, bool value) =>
            d.SetValue(BindPasswordProperty, value);

        private static bool GetUpdatingPassword(DependencyObject d) =>
            (bool)d.GetValue(UpdatingPasswordProperty);
        private static void SetUpdatingPassword(DependencyObject d, bool value) =>
            d.SetValue(UpdatingPasswordProperty, value);

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox box && !GetUpdatingPassword(box))
            {
                box.Password = e.NewValue as string ?? string.Empty;
            }
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox box)
            {
                if ((bool)e.OldValue)
                    box.PasswordChanged -= HandlePasswordChanged;

                if ((bool)e.NewValue)
                    box.PasswordChanged += HandlePasswordChanged;
            }
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox box)
            {
                SetUpdatingPassword(box, true);
                SetBoundPassword(box, box.Password);
                SetUpdatingPassword(box, false);
            }
        }
    }
}
