using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DiskGeek.App.ViewModels;

namespace DiskGeek.App.Controls;

/// <summary>
/// Custom-drawn treemap: renders <see cref="ItemsSource"/> (a collection of <see cref="FileSystemNodeViewModel"/>)
/// as proportionally-sized rectangles using a squarified layout, and raises <see cref="NodeClicked"/>
/// when the user clicks a rectangle so the host can drill down into it.
/// </summary>
public sealed class TreemapView : Control
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TreemapView, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Cap on how many of the largest items get their own rectangle, to keep small slivers legible.</summary>
    public static readonly StyledProperty<int> MaxItemsProperty =
        AvaloniaProperty.Register<TreemapView, int>(nameof(MaxItems), 60);

    /// <summary>
    /// Eight hues that stay far enough apart to tell neighbouring blocks apart, drawn from the same
    /// blue/teal/green/amber family as the rest of the app rather than a stock primary-colour set -
    /// the treemap is the loudest screen in DiskGeek and was the one that made it look like a
    /// different product.
    /// </summary>
    private static readonly IBrush[] Palette =
    {
        new SolidColorBrush(Color.Parse("#2E78D8")),
        new SolidColorBrush(Color.Parse("#17AEB7")),
        new SolidColorBrush(Color.Parse("#3BA55C")),
        new SolidColorBrush(Color.Parse("#E0A62B")),
        new SolidColorBrush(Color.Parse("#5B6EE1")),
        new SolidColorBrush(Color.Parse("#4E93B8")),
        new SolidColorBrush(Color.Parse("#B8734A")),
        new SolidColorBrush(Color.Parse("#6E7A91")),
    };

    // Gutters in the window background rather than white, so the blocks read as one surface.
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.Parse("#0A0D16")), 1.5);

    private static readonly IBrush EmptyTextBrush = new SolidColorBrush(Color.Parse("#7C8699"));

    private List<(FileSystemNodeViewModel Node, Rect Rect)> _layout = new();

    public event EventHandler<FileSystemNodeViewModel>? NodeClicked;

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int MaxItems
    {
        get => GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public TreemapView()
    {
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsSourceProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldIncc)
                oldIncc.CollectionChanged -= OnItemsCollectionChanged;
            if (change.NewValue is INotifyCollectionChanged newIncc)
                newIncc.CollectionChanged += OnItemsCollectionChanged;

            RecomputeLayout();
        }
        else if (change.Property == MaxItemsProperty || change.Property == BoundsProperty)
        {
            RecomputeLayout();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RecomputeLayout();

    private void RecomputeLayout()
    {
        var items = ItemsSource?.Cast<FileSystemNodeViewModel>().ToList() ?? new List<FileSystemNodeViewModel>();
        var top = items.OrderByDescending(i => i.SizeInBytes).Take(Math.Max(1, MaxItems)).ToList();

        _layout = Bounds.Width > 0 && Bounds.Height > 0
            ? TreemapLayout.Compute(top, i => (double)i.SizeInBytes, new Rect(Bounds.Size))
            : new List<(FileSystemNodeViewModel, Rect)>();

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_layout.Count == 0)
        {
            var message = new FormattedText(
                "Nothing to show yet — run a scan.",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                13,
                EmptyTextBrush);
            context.DrawText(message, new Point(8, 8));
            return;
        }

        for (var index = 0; index < _layout.Count; index++)
        {
            var (node, rect) = _layout[index];
            var brush = Palette[index % Palette.Length];

            context.FillRectangle(brush, rect);
            if (rect.Width > 2 && rect.Height > 2)
                context.DrawRectangle(null, BorderPen, rect);

            if (rect.Width > 40 && rect.Height > 18)
            {
                var label = $"{node.Name}\n{node.SizeDisplay}";
                var formatted = new FormattedText(
                    label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    Brushes.White)
                {
                    MaxTextWidth = Math.Max(1, rect.Width - 6),
                    MaxTextHeight = Math.Max(1, rect.Height - 4),
                    Trimming = TextTrimming.CharacterEllipsis
                };

                context.DrawText(formatted, new Point(rect.X + 4, rect.Y + 3));
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var position = e.GetPosition(this);
        foreach (var (node, rect) in _layout)
        {
            if (rect.Contains(position))
            {
                NodeClicked?.Invoke(this, node);
                break;
            }
        }
    }
}
