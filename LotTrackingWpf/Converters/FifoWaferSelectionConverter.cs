
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using LotTrackingWpf.Models;
using LotTrackingWpf.ViewModels;

namespace LotTrackingWpf.Converters;

public sealed class FifoWaferSelectionConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not Wafer wafer ||
            values[1] is not MainViewModel vm)
            return false;

        if (wafer.LotId != null || wafer.Status != "CREATED")
            return false;

        var selected = new HashSet<long>();

        foreach (string part in (vm.WaferIds ?? "").Split(
            new[] { ',', ';', ' ', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part, out long id))
                selected.Add(id);
        }

        if (selected.Contains(wafer.Id) ||
            selected.Count >= 24)
            return false;

        int alreadyInLot = vm.AssignmentLot?.TotalWafers ?? 0;

        if (alreadyInLot + selected.Count >= 24)
            return false;

        var next = vm.Wafers
            .Where(w => w.LotId == null &&
                        w.Status == "CREATED" &&
                        !selected.Contains(w.Id))
            .OrderBy(w => Number(w.WaferCode))
            .ThenBy(w => w.Id)
            .FirstOrDefault();

        return next?.Id == wafer.Id;
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static long Number(string? code)
    {
        if (code != null &&
            code.StartsWith("WAFER",
                StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(code.Substring(5), out long n) &&
            n > 0)
            return n;

        return long.MaxValue;
    }
}
