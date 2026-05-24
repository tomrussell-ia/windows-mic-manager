using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MicrophoneManager.WinUI.ViewModels;
using MicrophoneManager.WinUI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.UI;
using System.Collections.Specialized;

namespace MicrophoneManager.WinUI.Views;

public sealed partial class MicrophoneFlyout : UserControl
{
    public MicrophoneListViewModel ViewModel { get; }

    public event EventHandler? ViewportHeightChanged;

    private double? _cardOuterHeight;
    private readonly Dictionary<string, double> _cardOuterHeights = new(StringComparer.Ordinal);
    private bool _isUnloaded;

    public double? MeasuredCardOuterHeight => _cardOuterHeight;

    public bool IsDockedMode { get; set; }

    public Action? RequestClose { get; set; }

    public MicrophoneFlyout()
    {
        // Get ViewModel from DI
        var audioService = App.Host.Services.GetRequiredService<MicrophoneManager.WinUI.Services.IAudioDeviceService>();
        ViewModel = new MicrophoneListViewModel(audioService);

        InitializeComponent();

        Loaded += OnLoaded;
        ActualThemeChanged += OnActualThemeChanged;

        ViewModel.Microphones.CollectionChanged += Microphones_CollectionChanged;

        Unloaded += (s, e) =>
        {
            _isUnloaded = true;

            try { ViewModel.Microphones.CollectionChanged -= Microphones_CollectionChanged; } catch { }
            try { Loaded -= OnLoaded; } catch { }
            try { ActualThemeChanged -= OnActualThemeChanged; } catch { }
            try { ViewModel.Dispose(); } catch { }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.ApplyThemePalette(ActualTheme);
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        App.ApplyThemePalette(sender.ActualTheme);
    }

    private sealed class MeterSubscription
    {
        public MicrophoneEntryViewModel? ViewModel { get; set; }
        public PropertyChangedEventHandler? PropertyChangedHandler { get; set; }
        public List<Microsoft.UI.Xaml.Shapes.Rectangle>? TickRects { get; set; }
    }

    private readonly Dictionary<FrameworkElement, MeterSubscription> _meterSubscriptions = new();

    private void MeterHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isUnloaded) return;
        if (sender is not FrameworkElement host) return;

        if (!_meterSubscriptions.TryGetValue(host, out var subscription))
        {
            subscription = new MeterSubscription();
            _meterSubscriptions[host] = subscription;
        }

        if (host.DataContext is not MicrophoneEntryViewModel vm)
        {
            UpdateMeterVisuals(host, null);
            return;
        }

        if (ReferenceEquals(subscription.ViewModel, vm))
        {
            UpdateMeterVisuals(host, vm);
            return;
        }

        UnsubscribeMeter(host);
        SubscribeMeter(host, vm);
        UpdateMeterVisuals(host, vm);
    }

    private void MeterHost_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement host) return;
        UnsubscribeMeter(host);
        _meterSubscriptions.Remove(host);
    }

    private void MeterHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUnloaded) return;
        if (sender is not FrameworkElement host) return;
        UpdateMeterVisuals(host, host.DataContext as MicrophoneEntryViewModel);
    }

    private void SubscribeMeter(FrameworkElement host, MicrophoneEntryViewModel vm)
    {
        if (!_meterSubscriptions.TryGetValue(host, out var subscription))
        {
            subscription = new MeterSubscription();
            _meterSubscriptions[host] = subscription;
        }

        PropertyChangedEventHandler handler = (_, args) =>
        {
            if (_isUnloaded) return;
            if (args.PropertyName == nameof(MicrophoneEntryViewModel.InputLevelPercent) ||
                args.PropertyName == nameof(MicrophoneEntryViewModel.PeakLevelPercent))
            {
                host.DispatcherQueue.TryEnqueue(() => UpdateMeterVisuals(host, vm));
            }
        };

        subscription.ViewModel = vm;
        subscription.PropertyChangedHandler = handler;
        vm.PropertyChanged += handler;
    }

    private void UnsubscribeMeter(FrameworkElement host)
    {
        if (!_meterSubscriptions.TryGetValue(host, out var subscription)) return;
        if (subscription.ViewModel == null || subscription.PropertyChangedHandler == null) return;

        try
        {
            subscription.ViewModel.PropertyChanged -= subscription.PropertyChangedHandler;
        }
        catch
        {
        }

        subscription.ViewModel = null;
        subscription.PropertyChangedHandler = null;
    }

    private static void UpdateMeterVisuals(FrameworkElement host, MicrophoneEntryViewModel? vm)
    {
        const double visibleMinDb = -60.0;
        const double warningDb = -20.0;
        const double errorDb = -9.0;
        var barWidth = Math.Max(1.0, host.ActualWidth);
        var barHeight = Math.Max(1.0, host.ActualHeight);

        var zoneGreen = host.FindName("ZoneGreen") as Microsoft.UI.Xaml.Shapes.Rectangle;
        var zoneYellow = host.FindName("ZoneYellow") as Microsoft.UI.Xaml.Shapes.Rectangle;
        var zoneRed = host.FindName("ZoneRed") as Microsoft.UI.Xaml.Shapes.Rectangle;
        var tickCanvas = host.FindName("TickCanvas") as Microsoft.UI.Xaml.Controls.Canvas;

        if (host.FindName("MeterFill") is not Microsoft.UI.Xaml.Shapes.Rectangle fill) return;
        if (host.FindName("PeakMarker") is not Microsoft.UI.Xaml.Shapes.Rectangle marker) return;

        fill.Width = barWidth;
        marker.Width = barWidth;
        if (zoneGreen != null) zoneGreen.Width = barWidth;
        if (zoneYellow != null) zoneYellow.Width = barWidth;
        if (zoneRed != null) zoneRed.Width = barWidth;
        if (tickCanvas != null)
        {
            tickCanvas.Width = barWidth;
            tickCanvas.Height = barHeight;
        }

        if (host.ActualWidth <= 0 || host.ActualHeight <= 0)
        {
            fill.Height = 0;
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(marker, 0);
            return;
        }

        static double RescaleObsPercentToVisible(double obsPercent)
        {
            var minObsPercent = ObsMeterMath.DbToPercent(visibleMinDb);
            obsPercent = Math.Clamp(obsPercent, 0.0, 100.0);
            if (obsPercent <= minObsPercent) return 0.0;
            return Math.Clamp((obsPercent - minObsPercent) / (100.0 - minObsPercent) * 100.0, 0.0, 100.0);
        }

        void SetZone(Microsoft.UI.Xaml.Shapes.Rectangle? rect, double lowerPercentVisible, double upperPercentVisible)
        {
            if (rect == null) return;
            lowerPercentVisible = Math.Clamp(lowerPercentVisible, 0.0, 100.0);
            upperPercentVisible = Math.Clamp(upperPercentVisible, 0.0, 100.0);
            if (upperPercentVisible < lowerPercentVisible)
            {
                (lowerPercentVisible, upperPercentVisible) = (upperPercentVisible, lowerPercentVisible);
            }

            var zoneHeight = barHeight * ((upperPercentVisible - lowerPercentVisible) / 100.0);
            var zoneTop = barHeight * (1.0 - (upperPercentVisible / 100.0));
            rect.Height = Math.Max(0.0, zoneHeight);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(rect, Math.Clamp(zoneTop, 0.0, Math.Max(0.0, barHeight - rect.Height)));
        }

        // Background zones: [-60..-20]=green, [-20..-9]=yellow, [-9..0]=red.
        var minP = RescaleObsPercentToVisible(ObsMeterMath.DbToPercent(visibleMinDb));
        var warnP = RescaleObsPercentToVisible(ObsMeterMath.DbToPercent(warningDb));
        var errP = RescaleObsPercentToVisible(ObsMeterMath.DbToPercent(errorDb));
        var maxP = 100.0;
        SetZone(zoneGreen, minP, warnP);
        SetZone(zoneYellow, warnP, errP);
        SetZone(zoneRed, errP, maxP);

        // Tick marks at major dB levels.
        if (tickCanvas != null)
        {
            if (tickCanvas.Children.Count == 0)
            {
                // Major ticks inspired by OBS labels (-60..0).
                var tickDbs = new[] { -60.0, -50.0, -40.0, -30.0, -25.0, -20.0, -15.0, -10.0, -5.0, 0.0 };
                foreach (var _ in tickDbs)
                {
                    var tick = new Microsoft.UI.Xaml.Shapes.Rectangle
                    {
                        Width = barWidth,
                        Height = 1,
                        Fill = (Brush)Application.Current.Resources["ForegroundBrush"],
                        Opacity = 0.35,
                        IsHitTestVisible = false
                    };
                    tickCanvas.Children.Add(tick);
                }
            }

            var tickDbsUpdate = new[] { -60.0, -50.0, -40.0, -30.0, -25.0, -20.0, -15.0, -10.0, -5.0, 0.0 };
            var count = Math.Min(tickCanvas.Children.Count, tickDbsUpdate.Length);
            for (var i = 0; i < count; i++)
            {
                if (tickCanvas.Children[i] is not Microsoft.UI.Xaml.Shapes.Rectangle tick) continue;
                tick.Width = barWidth;
                var tickPercent = RescaleObsPercentToVisible(ObsMeterMath.DbToPercent(tickDbsUpdate[i]));
                var tickY = (barHeight * (1.0 - (tickPercent / 100.0))) - (tick.Height / 2.0);
                tickY = Math.Clamp(tickY, 0.0, Math.Max(0.0, barHeight - tick.Height));
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(tick, tickY);
            }
        }

        // Live fill + peak marker use the same visible min scale.
        var inputPercentObs = vm?.InputLevelPercent ?? 0.0;
        var peakPercentObs = vm?.PeakLevelPercent ?? 0.0;

        var inputPercent = RescaleObsPercentToVisible(inputPercentObs);
        var peakPercent = RescaleObsPercentToVisible(peakPercentObs);

        fill.Height = barHeight * (inputPercent / 100.0);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(fill, Math.Max(0.0, barHeight - fill.Height));

        var markerHeight = marker.Height;
        var markerY = (barHeight * (1.0 - (peakPercent / 100.0))) - (markerHeight / 2.0);
        markerY = Math.Clamp(markerY, 0.0, Math.Max(0.0, barHeight - markerHeight));
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(marker, markerY);
    }

    private void Microphones_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var activeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var microphone in ViewModel.Microphones)
        {
            if (!string.IsNullOrWhiteSpace(microphone.Id))
            {
                activeIds.Add(microphone.Id);
            }
        }

        if (_cardOuterHeights.Count > 0)
        {
            var staleIds = new List<string>();
            foreach (var id in _cardOuterHeights.Keys)
            {
                if (!activeIds.Contains(id))
                {
                    staleIds.Add(id);
                }
            }

            foreach (var id in staleIds)
            {
                _cardOuterHeights.Remove(id);
            }
        }

        RefreshMeasuredCardOuterHeight();
        UpdateViewportHeight();
    }

    private void MicrophoneCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isUnloaded) return;

        // Capture an accurate per-card outer height (including margin) so the window
        // can reliably size itself even when ScrollViewer/DesiredSize is misleading.
        TryCaptureCardOuterHeight(sender as FrameworkElement);

        // Notify window to resize whenever a card finishes loading
        // (content size may have changed)
        ViewportHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MicrophoneCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUnloaded) return;

        // Deadband: ignore sub-pixel height changes to prevent infinite resize loops.
        // When the window resizes, cards may re-layout with tiny height deltas that
        // would otherwise trigger another resize.
        if (Math.Abs(e.NewSize.Height - e.PreviousSize.Height) < 2.0 &&
            e.PreviousSize.Height > 0)
        {
            return;
        }

        var updated = TryCaptureCardOuterHeight(sender as FrameworkElement);
        if (updated)
        {
            ViewportHeightChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool TryCaptureCardOuterHeight(FrameworkElement? element)
    {
        if (element == null) return false;
        if (element.ActualHeight <= 0) return false;

        var margin = element.Margin;
        var outer = element.ActualHeight + margin.Top + margin.Bottom;
        if (outer <= 0) return false;

        var viewModel = element.DataContext as MicrophoneEntryViewModel;
        var id = viewModel?.Id;

        var updated = false;
        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!_cardOuterHeights.TryGetValue(id, out var existing) || Math.Abs(existing - outer) > 0.5)
            {
                _cardOuterHeights[id] = outer;
                updated = true;
            }
        }

        // Track the largest realized card as a fallback while some cards are still unmeasured.
        // Use a 2px deadband to prevent sub-pixel jitter from triggering endless resizes.
        if (_cardOuterHeight == null || outer > _cardOuterHeight.Value + 2.0)
        {
            _cardOuterHeight = outer;
            App.Trace($"Measured card outer height={outer:0.0}");
            updated = true;
        }

        if (updated)
        {
            RefreshMeasuredCardOuterHeight();
        }

        return updated;
    }

    private void RefreshMeasuredCardOuterHeight()
    {
        double? max = null;
        foreach (var height in _cardOuterHeights.Values)
        {
            if (max == null || height > max.Value)
            {
                max = height;
            }
        }

        _cardOuterHeight = max;
    }

    private static T? FindFirstDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;

            var nested = FindFirstDescendant<T>(child);
            if (nested != null) return nested;
        }

        return null;
    }

    private void UpdateViewportHeight()
    {
        if (_isUnloaded) return;

        // No explicit height management - let the ItemsControl size naturally.
        // The containing MicrophoneWindow will measure our DesiredSize and
        // clamp to screen bounds as needed.
        ViewportHeightChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the desired size for the flyout content, measuring the actual ItemsControl
    /// content rather than relying on ScrollViewer (which fills available space).
    /// </summary>
    public Windows.Foundation.Size GetDesiredContentSize()
    {
        var desiredHeight = GetDesiredContentHeight();
        var desiredWidth = GetDesiredContentWidth();
        return new Windows.Foundation.Size(desiredWidth, desiredHeight);
    }

    public double GetDesiredContentWidth()
    {
        // Each card is Width="116" with Margin left=3 = 119px per card.
        // Add 8px slack: DIP-to-pixel rounding at non-integer DPI scales (e.g. 125%) can
        // cause the physical window to be 2-4px narrower than the cards need, triggering
        // a horizontal scrollbar on a window that is already sized to fit its content.
        const double cardOuterWidth = 119.0;
        const double widthSlack = 8.0;
        var padding = RootGrid?.Padding ?? new Thickness(0);
        var count = Math.Max(1, ViewModel.Microphones.Count);
        return padding.Left + padding.Right + (count * cardOuterWidth) + widthSlack;
    }

    public double GetDesiredContentHeight()
    {
        const double layoutSlack = 2.0;

        // Base padding of the flyout root grid.
        var padding = RootGrid?.Padding ?? new Thickness(0);
        var baseHeight = padding.Top + padding.Bottom + layoutSlack;

        // Header row outer height (ActualHeight + Margin).
        if (HeaderRow != null)
        {
            var m = HeaderRow.Margin;
            baseHeight += HeaderRow.ActualHeight + m.Top + m.Bottom;
        }

        var count = ViewModel.Microphones.Count;

        // Empty state row, only if visible.
        if (count <= 0)
        {
            if (EmptyStateText != null && EmptyStateText.Visibility == Visibility.Visible)
            {
                var m = EmptyStateText.Margin;
                baseHeight += EmptyStateText.ActualHeight + m.Top + m.Bottom;
            }

            // Reasonable minimum when empty.
            return Math.Max(baseHeight, 120);
        }

        // If we haven't measured a card yet, fall back to something sensible.
        if (_cardOuterHeight == null || _cardOuterHeight.Value <= 0)
        {
            // Try to measure the first realized card via the ItemsControl container.
            // This is a safety net in case Loaded/SizeChanged runs before ActualHeight is stable.
            try
            {
                var container = MicrophoneList?.ContainerFromIndex(0) as DependencyObject;
                if (container != null)
                {
                    var border = FindFirstDescendant<Microsoft.UI.Xaml.Controls.Border>(container);
                    TryCaptureCardOuterHeight(border);
                }
            }
            catch
            {
            }

            // If still unknown, fall back to estimated single-card height.
            if (_cardOuterHeight == null || _cardOuterHeight.Value <= 0)
            {
                return Math.Max(baseHeight + 380, 380);
            }
        }

        // Horizontal mixer: height = tallest single card, not the sum of all cards.
        return baseHeight + _cardOuterHeight.Value;
    }

    private void DockButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsDockedMode)
        {
            // Already docked, undock
            App.DockedWindow?.Close();
            App.DockedWindow = null;
        }
        else
        {
            // Create docked window as a normal resizable window (not a popup)
            var dockedWindow = new MicrophoneWindow(isDocked: true)
            {
                Title = "Microphone Manager"
            };

            App.DockedWindow = dockedWindow;
            dockedWindow.Activate();

            // Close the current popup (if opened from tray)
            RequestClose?.Invoke();
        }
    }

    private void DismissError_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DismissError();
    }
}

// Extension methods for MicrophoneEntryViewModel to add helper functions
public static class MicrophoneViewModelExtensions
{
    // Helper functions for x:Bind in DataTemplate
    public static string GetMuteIcon(this MicrophoneEntryViewModel vm, bool isMuted)
    {
        return isMuted ? "\uE74F" : "\uE720";
    }

    public static string GetMuteTooltip(this MicrophoneEntryViewModel vm, bool isMuted)
    {
        return isMuted ? "Unmute" : "Mute";
    }

    public static Color GetDefaultButtonColor(this MicrophoneEntryViewModel vm, bool isDefault)
    {
        return isDefault
            ? Color.FromArgb(255, 30, 136, 229) // Blue
            : Color.FromArgb(255, 61, 61, 61);   // Gray
    }

    public static Color GetCommButtonColor(this MicrophoneEntryViewModel vm, bool isDefaultComm)
    {
        return isDefaultComm
            ? Color.FromArgb(255, 106, 27, 154) // Purple
            : Color.FromArgb(255, 61, 61, 61);   // Gray
    }
}
