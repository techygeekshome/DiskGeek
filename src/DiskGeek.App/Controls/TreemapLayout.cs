using Avalonia;

namespace DiskGeek.App.Controls;

/// <summary>
/// Squarified treemap layout (Bruls, Huizing &amp; van Wijk, 2000): recursively lays items into rows
/// chosen to keep rectangle aspect ratios as close to square as possible, which is what makes a
/// treemap easy to scan visually compared to a naive slice-and-dice layout.
/// </summary>
public static class TreemapLayout
{
    public static List<(T Item, Rect Rect)> Compute<T>(
        IReadOnlyList<T> items,
        Func<T, double> valueSelector,
        Rect bounds)
    {
        var result = new List<(T, Rect)>();
        if (items.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return result;

        // Zero/negative-size items have no area to give them and would blow up the aspect-ratio
        // math (division by a min area of 0), so they're excluded from the visual layout entirely.
        var positive = items.Where(i => valueSelector(i) > 0).OrderByDescending(valueSelector).ToList();
        if (positive.Count == 0)
            return result;

        var total = positive.Sum(valueSelector);
        var areaTotal = bounds.Width * bounds.Height;

        var scaled = positive
            .Select(i => new ScaledItem<T>(i, valueSelector(i) / total * areaTotal))
            .ToList();

        SquarifyRecursive(scaled, new List<ScaledItem<T>>(), bounds, result);
        return result;
    }

    private sealed record ScaledItem<T>(T Item, double Area);

    private static void SquarifyRecursive<T>(
        List<ScaledItem<T>> remaining,
        List<ScaledItem<T>> row,
        Rect bounds,
        List<(T, Rect)> result)
    {
        if (remaining.Count == 0)
        {
            if (row.Count > 0)
                LayoutRow(row, bounds, result);
            return;
        }

        var shortSide = Math.Min(bounds.Width, bounds.Height);
        var next = remaining[0];
        var rowWithNext = new List<ScaledItem<T>>(row) { next };

        if (row.Count == 0 || Worst(row, shortSide) >= Worst(rowWithNext, shortSide))
        {
            remaining.RemoveAt(0);
            SquarifyRecursive(remaining, rowWithNext, bounds, result);
        }
        else
        {
            var remainingBounds = LayoutRow(row, bounds, result);
            SquarifyRecursive(remaining, new List<ScaledItem<T>>(), remainingBounds, result);
        }
    }

    /// <summary>Worst (highest) aspect ratio that would result from laying out this row at the given side length.</summary>
    private static double Worst<T>(List<ScaledItem<T>> row, double sideLength)
    {
        if (row.Count == 0) return double.MaxValue;

        var sum = row.Sum(r => r.Area);
        var max = row.Max(r => r.Area);
        var min = row.Min(r => r.Area);
        var sideSq = sideLength * sideLength;
        var sumSq = sum * sum;

        if (min <= 0 || sumSq <= 0) return double.MaxValue;

        return Math.Max(sideSq * max / sumSq, sumSq / (sideSq * min));
    }

    /// <summary>Lays one row of items along the shorter side of <paramref name="bounds"/>, returns the leftover rect.</summary>
    private static Rect LayoutRow<T>(List<ScaledItem<T>> row, Rect bounds, List<(T, Rect)> result)
    {
        var rowSum = row.Sum(r => r.Area);

        if (bounds.Width < bounds.Height)
        {
            var rowHeight = rowSum / bounds.Width;
            var x = bounds.X;
            foreach (var item in row)
            {
                var w = item.Area / rowHeight;
                result.Add((item.Item, new Rect(x, bounds.Y, w, rowHeight)));
                x += w;
            }
            return new Rect(bounds.X, bounds.Y + rowHeight, bounds.Width, Math.Max(0, bounds.Height - rowHeight));
        }
        else
        {
            var rowWidth = rowSum / bounds.Height;
            var y = bounds.Y;
            foreach (var item in row)
            {
                var h = item.Area / rowWidth;
                result.Add((item.Item, new Rect(bounds.X, y, rowWidth, h)));
                y += h;
            }
            return new Rect(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.Width - rowWidth), bounds.Height);
        }
    }
}
