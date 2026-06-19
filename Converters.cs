using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZebraPrintUtility.Models;

namespace ZebraPrintUtility
{
    // 1. MethodToVisibilityConverter
    // Handles Visibility and RadioButton two-way IsChecked bindings
    public class MethodToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return targetType == typeof(Visibility) ? Visibility.Collapsed : false;

            string paramStr = parameter.ToString()!;
            string valStr = value.ToString()!;
            bool match = string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);

            if (targetType == typeof(Visibility))
            {
                return match ? Visibility.Visible : Visibility.Collapsed;
            }
            
            return match;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                string paramStr = parameter.ToString()!;
                if (Enum.TryParse(targetType, paramStr, true, out object? result))
                {
                    return result!;
                }
            }
            return Binding.DoNothing;
        }
    }

    // 2. NullToVisibilityConverter
    // Handles object null checks and numeric count checks (e.g. 0 items)
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isNull = value == null;

            if (value is int intValue)
            {
                isNull = intValue == 0;
            }
            else if (value is double dblValue)
            {
                isNull = dblValue == 0.0;
            }

            bool invert = parameter?.ToString() == "Invert";
            bool isVisible = invert ? isNull : !isNull;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 3. BooleanToVisibilityConverter
    // Standard boolean to visibility
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter?.ToString() == "Invert";
                bool isVisible = invert ? !boolValue : boolValue;
                return isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
            {
                bool isVisible = vis == Visibility.Visible;
                bool invert = parameter?.ToString() == "Invert";
                return invert ? !isVisible : isVisible;
            }
            return false;
        }
    }
}
