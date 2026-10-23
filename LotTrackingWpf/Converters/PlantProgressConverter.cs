
using System;
using System.Globalization;
using System.Windows.Data;

namespace LotTrackingWpf.Converters;

public sealed class PlantProgressConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 2)
            return 0.0;

        int plantNumber = values[0] is int number
            ? number
            : 0;

        string status = values[1]?.ToString() ?? "";

        // A completed lot always displays 100%.
        if (status.Equals(
            "COMPLETED",
            StringComparison.OrdinalIgnoreCase))
        {
            return 100.0;
        }

        // Lots that have not started remain at 0%.
        if (status.Equals("CREATED",
                StringComparison.OrdinalIgnoreCase) ||
            status.Equals("READY_FOR_PROCESSING",
                StringComparison.OrdinalIgnoreCase) ||
            status.Equals("WAITING",
                StringComparison.OrdinalIgnoreCase))
        {
            return 0.0;
        }

        // Show milestone progress through the 10 plants.
        if (plantNumber <= 0)
            return 0.0;

        if (plantNumber <= 2)
            return 25.0;

        if (plantNumber <= 5)
            return 50.0;

        if (plantNumber <= 7)
            return 75.0;

        // Plant 8, 9, or 10: remain at 75%
        // until the lot is actually completed.
        return 75.0;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException(
            "Progress conversion is one-way only.");
    }
}
