using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FreelanceApp.Converters
{
    public sealed class StatusCodeToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string code || string.IsNullOrWhiteSpace(code))
                return value;

            var kind = parameter as string ?? "Status";

            string resourceKey = kind switch
            {
                "Filter" => code switch
                {
                    "new"         => "AdminComplaints_Filter_Status_New",
                    "in_progress" => "AdminComplaints_Filter_Status_InProgress",
                    "resolved"    => "AdminComplaints_Filter_Status_Resolved",
                    "dismissed"   => "AdminComplaints_Filter_Status_Dismissed",
                    "all"         => "AdminComplaints_Filter_Status_All",
                    _             => code
                },
                _ => code switch
                {
                    "new"         => "AdminComplaints_Status_New",
                    "in_progress" => "AdminComplaints_Status_InProgress",
                    "resolved"    => "AdminComplaints_Status_Resolved",
                    "dismissed"   => "AdminComplaints_Status_Dismissed",
                    _             => code
                }
            };

            if (resourceKey == code)
                return code;

            var localized = Application.Current.TryFindResource(resourceKey) as string;
            return localized ?? code;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}

