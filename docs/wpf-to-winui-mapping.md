# WPF to WinUI 3 Control & API Mapping Guide

**Date**: 2026-01-04
**Purpose**: Quick reference for migrating WPF controls, APIs, and patterns to WinUI 3

## Overview

This document provides side-by-side comparisons of WPF and WinUI 3 equivalents for controls, XAML syntax, APIs, and patterns used in the Microphone Manager application.

---

## Table of Contents

1. [XAML Namespace & Syntax](#1-xaml-namespace--syntax)
2. [Controls](#2-controls)
3. [Layout Containers](#3-layout-containers)
4. [Data Binding](#4-data-binding)
5. [Styles & Templates](#5-styles--templates)
6. [Triggers → VisualStates](#6-triggers--visualstates)
7. [Value Converters](#7-value-converters)
8. [Threading & Dispatcher](#8-threading--dispatcher)
9. [Application Lifecycle](#9-application-lifecycle)
10. [Window Management](#10-window-management)
11. [Resources & Theming](#11-resources--theming)
12. [Icons & Fonts](#12-icons--fonts)
13. [Common Gotchas](#13-common-gotchas)

---

## 1. XAML Namespace & Syntax

### Namespaces

| WPF | WinUI 3 |
|-----|---------|
| `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` | `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` *(same)* |
| `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"` | `xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"` *(same)* |
| `xmlns:local="clr-namespace:MicrophoneManager"` | `xmlns:local="using:MicrophoneManager"` |
| `xmlns:sys="clr-namespace:System;assembly=mscorlib"` | `xmlns:sys="using:System"` |

**Key Difference**: `clr-namespace:` → `using:`

### x:Bind vs Binding

| WPF | WinUI 3 |
|-----|---------|
| `{Binding PropertyName}` | `{x:Bind PropertyName, Mode=OneWay}` *(preferred, compiled)* |
| `{Binding PropertyName, Mode=TwoWay}` | `{x:Bind PropertyName, Mode=TwoWay}` |
| Default mode: `OneWay` | Default mode: `OneTime` *(must specify OneWay/TwoWay)* |
| Runtime binding | Compile-time binding (type-safe) |

**DataTemplate Requirement** in WinUI 3:
```xml
<DataTemplate x:DataType="viewmodels:MicrophoneEntryViewModel">
    <TextBlock Text="{x:Bind Name, Mode=OneWay}"/>
</DataTemplate>
```

### Null-Conditional Binding

| WPF | WinUI 3 |
|-----|---------|
| `{Binding Path=Property.SubProperty}` *(auto null-check)* | `{x:Bind Property.SubProperty, Mode=OneWay, FallbackValue=''}` |

---

## 2. Controls

### Basic Controls

| WPF Control | WinUI 3 Equivalent | Notes |
|-------------|-------------------|-------|
| `Button` | `Button` | Same |
| `TextBlock` | `TextBlock` | Same |
| `TextBox` | `TextBox` | Same |
| `CheckBox` | `CheckBox` | Same |
| `RadioButton` | `RadioButton` | Same |
| `Slider` | `Slider` | Same API, different default style |
| `ComboBox` | `ComboBox` | Same |
| `ListBox` | `ListBox` or `ListView` | Use ListView for modern look |
| `ListView` | `ListView` | Same |
| `ProgressBar` | `ProgressBar` or `ProgressRing` | |
| `Border` | `Border` | Same |
| `Image` | `Image` | Different source types |

### Specialized Controls

| WPF Control | WinUI 3 Equivalent | Notes |
|-------------|-------------------|-------|
| `ContextMenu` | `MenuFlyout` | Attached via `FlyoutBase.AttachedFlyout` |
| `ToolTip` | Use `ToolTipService.ToolTip` | Attached property, not direct control |
| `Popup` | `Flyout` or `Popup` | Prefer Flyout for modern UI |
| `Menu` / `MenuItem` | `MenuBar` / `MenuFlyoutItem` | Different hierarchy |
| `DataGrid` | `DataGrid` *(CommunityToolkit.WinUI)* | Not built-in, needs package |

### Icon Display

| WPF | WinUI 3 |
|-----|---------|
| `<TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE720;"/>` | `<FontIcon Glyph="&#xE720;"/>` *(preferred)* |
| | `<FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE720;"/>` |
| | `<SymbolIcon Symbol="Microphone"/>` *(for common icons)* |
| | `<BitmapIcon UriSource="/Assets/icon.png"/>` |

---

## 3. Layout Containers

### Common Layouts

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `Grid` | `Grid` | Same |
| `StackPanel` | `StackPanel` | Add `Spacing` property (easier than Margin) |
| `WrapPanel` | `ItemsRepeater` + `UniformGridLayout` | No direct WrapPanel |
| `DockPanel` | *(None)* | Use Grid with row/column definitions |
| `Canvas` | `Canvas` | Same |
| `ScrollViewer` | `ScrollViewer` | Same |
| `UniformGrid` | `Grid` with equal columns | No UniformGrid control |

**StackPanel Spacing** (WinUI 3):
```xml
<!-- WPF: Use Margin on each child -->
<StackPanel>
    <TextBlock Margin="0,0,0,8"/>
    <Button Margin="0,0,0,8"/>
</StackPanel>

<!-- WinUI 3: Use Spacing property -->
<StackPanel Spacing="8">
    <TextBlock/>
    <Button/>
</StackPanel>
```

---

## 4. Data Binding

### Binding Modes

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `Mode=OneWay` | `Mode=OneWay` | Same |
| `Mode=TwoWay` | `Mode=TwoWay` | Same |
| `Mode=OneTime` | `Mode=OneTime` | Same |
| `Mode=OneWayToSource` | *(Not supported in x:Bind)* | Use events instead |
| `UpdateSourceTrigger=PropertyChanged` | *(Default behavior)* | No need to specify |

### RelativeSource Bindings

| WPF | WinUI 3 |
|-----|---------|
| `{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=ViewModel}` | `{x:Bind ViewModel, Mode=OneWay}` *(if in UserControl code-behind)* |
| `{Binding ElementName=SomeElement, Path=ActualWidth}` | `{x:Bind SomeElement.ActualWidth, Mode=OneWay}` |

### MultiBinding

| WPF | WinUI 3 |
|-----|---------|
| `<MultiBinding Converter="{StaticResource MyConverter}">...</MultiBinding>` | Use `x:Bind` with function: `{x:Bind MyFunction(Prop1, Prop2), Mode=OneWay}` |
| | Or use `CommunityToolkit.WinUI` MultiBindingExtension |

**Example**:
```xml
<!-- WPF -->
<TextBlock>
    <TextBlock.Text>
        <MultiBinding StringFormat="{}{0} x {1}">
            <Binding Path="Width"/>
            <Binding Path="Height"/>
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>

<!-- WinUI 3 with x:Bind function -->
<TextBlock Text="{x:Bind GetDimensions(Width, Height), Mode=OneWay}"/>

<!-- Code-behind -->
public string GetDimensions(double width, double height) => $"{width} x {height}";
```

---

## 5. Styles & Templates

### Basic Style

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `<Style TargetType="Button">` | `<Style TargetType="Button">` | Same |
| `<Setter Property="Background" Value="Red"/>` | `<Setter Property="Background" Value="Red"/>` | Same |
| `BasedOn="{StaticResource BaseStyle}"` | `BasedOn="{StaticResource BaseStyle}"` | Same |

### ControlTemplate

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `<ControlTemplate TargetType="Button">` | `<ControlTemplate TargetType="Button">` | Same structure |
| `{TemplateBinding Property}` | `{TemplateBinding Property}` | Same |
| `<ContentPresenter/>` | `<ContentPresenter/>` | Same |

**Key Difference**: Triggers → VisualStates (see section 6)

---

## 6. Triggers → VisualStates

### Property Triggers

**WPF (Triggers)**:
```xml
<Style TargetType="Button">
    <Setter Property="Background" Value="Gray"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="LightGray"/>
        </Trigger>
        <Trigger Property="IsPressed" Value="True">
            <Setter Property="Background" Value="DarkGray"/>
        </Trigger>
    </Style.Triggers>
</Style>
```

**WinUI 3 (VisualStates)**:
```xml
<Style TargetType="Button">
    <Setter Property="Background" Value="Gray"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Grid x:Name="RootGrid" Background="{TemplateBinding Background}">

                    <VisualStateManager.VisualStateGroups>
                        <VisualStateGroup x:Name="CommonStates">

                            <VisualState x:Name="Normal"/>

                            <VisualState x:Name="PointerOver">
                                <VisualState.Setters>
                                    <Setter Target="RootGrid.Background" Value="LightGray"/>
                                </VisualState.Setters>
                            </VisualState>

                            <VisualState x:Name="Pressed">
                                <VisualState.Setters>
                                    <Setter Target="RootGrid.Background" Value="DarkGray"/>
                                </VisualState.Setters>
                            </VisualState>

                        </VisualStateGroup>
                    </VisualStateManager.VisualStateGroups>

                    <ContentPresenter/>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

**Common VisualState Names**:
- `Normal`, `PointerOver`, `Pressed`, `Disabled` (buttons)
- `Selected`, `PointerOverSelected` (list items)
- `Checked`, `Unchecked` (checkboxes)
- `Focused`, `Unfocused` (input controls)

### DataTriggers

**WPF (DataTrigger)**:
```xml
<Style.Triggers>
    <DataTrigger Binding="{Binding IsActive}" Value="True">
        <Setter Property="Foreground" Value="Green"/>
    </DataTrigger>
</Style.Triggers>
```

**WinUI 3 (StateTrigger)**:
```xml
<VisualState x:Name="ActiveState">
    <VisualState.StateTriggers>
        <StateTrigger IsActive="{x:Bind ViewModel.IsActive, Mode=OneWay}"/>
    </VisualState.StateTriggers>
    <VisualState.Setters>
        <Setter Target="TextBlock.Foreground" Value="Green"/>
    </VisualState.Setters>
</VisualState>
```

**Alternative**: Use `x:Bind` with converter or function directly in property:
```xml
<TextBlock Foreground="{x:Bind GetForeground(ViewModel.IsActive), Mode=OneWay}"/>
```

---

## 7. Value Converters

### Interface Difference

**WPF**:
```csharp
using System.Windows.Data;
using System.Globalization;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
```

**WinUI 3**:
```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
```

**Key Difference**: `CultureInfo culture` → `string language`

### Built-in Converters

| WPF | WinUI 3 |
|-----|---------|
| `BooleanToVisibilityConverter` *(built-in)* | *(Not built-in, create custom)* |
| `NullToVisibilityConverter` | *(Not built-in, create custom)* |

**Tip**: Use `x:Bind` with functions instead of converters when possible:
```xml
<!-- Instead of converter -->
<TextBlock Visibility="{x:Bind ToVisibility(ViewModel.IsVisible), Mode=OneWay}"/>

<!-- Code-behind -->
public Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
```

---

## 8. Threading & Dispatcher

### Dispatcher Access

| WPF | WinUI 3 |
|-----|---------|
| `Dispatcher.BeginInvoke(action)` | `DispatcherQueue.TryEnqueue(action)` |
| `Dispatcher.Invoke(action)` | `DispatcherQueue.TryEnqueue(action)` *(no synchronous version)* |
| `Application.Current.Dispatcher` | `DispatcherQueue.GetForCurrentThread()` |
| `Dispatcher.CheckAccess()` | `DispatcherQueue.HasThreadAccess` |

**Example Migration**:
```csharp
// WPF
private void OnDeviceChanged()
{
    Application.Current.Dispatcher.BeginInvoke(() => RefreshDevices());
}

// WinUI 3
private void OnDeviceChanged()
{
    DispatcherQueue.GetForCurrentThread().TryEnqueue(() => RefreshDevices());
}
```

### DispatcherTimer

| WPF | WinUI 3 |
|-----|---------|
| `new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }` | `DispatcherQueue.CreateTimer()` |
| `timer.Tick += Handler;` | `timer.Tick += Handler;` |
| `timer.Start()` | `timer.Start()` |
| `DispatcherPriority` enum | *(No priority control)* |

**Example Migration**:
```csharp
// WPF
var timer = new DispatcherTimer(DispatcherPriority.Background)
{
    Interval = TimeSpan.FromMilliseconds(16)
};
timer.Tick += (s, e) => UpdateMeter();
timer.Start();

// WinUI 3
var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
timer.Interval = TimeSpan.FromMilliseconds(16);
timer.Tick += (s, e) => UpdateMeter();
timer.Start();
```

---

## 9. Application Lifecycle

### Application Class

| WPF | WinUI 3 |
|-----|---------|
| `public partial class App : Application` | `public partial class App : Application` *(same base class)* |
| `Startup` event | `OnLaunched` override |
| `Exit` event | `OnLaunched` override (no explicit Exit event) |
| `App.xaml` with `Startup="Handler"` | No event attributes, use override |
| `ShutdownMode="OnExplicitShutdown"` | *(Not available, handle lifecycle manually)* |

**Example Migration**:
```csharp
// WPF App.xaml.cs
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

// WinUI 3 App.xaml.cs
public partial class App : Application
{
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }

    private Window? m_window;
}
```

### Shutdown

| WPF | WinUI 3 |
|-----|---------|
| `Application.Current.Shutdown()` | *(No direct equivalent)* |
| | Close all windows manually, then exit process |

```csharp
// WinUI 3 shutdown
foreach (var window in GetOpenWindows())
{
    window.Close();
}
// App will exit when all windows closed (or call Process.GetCurrentProcess().Kill() for immediate)
```

---

## 10. Window Management

### Window Properties

| WPF Property | WinUI 3 Equivalent | Notes |
|--------------|-------------------|-------|
| `Window.Title` | `Window.Title` | Same |
| `Window.Width` / `Height` | `AppWindow.Resize(new SizeInt32(w, h))` | Different API |
| `Window.Left` / `Top` | `AppWindow.Move(new PointInt32(x, y))` | Different API |
| `Window.WindowStyle="None"` | `AppWindow.TitleBar.ExtendsContentIntoTitleBar = true` | More control |
| `Window.AllowsTransparency` | Use `SystemBackdrop` (Mica, Acrylic) | Different approach |
| `Window.ShowInTaskbar` | `AppWindow.IsShownInSwitchers = false` | |
| `Window.Topmost` | `OverlappedPresenter.IsAlwaysOnTop = true` | Via presenter |
| `Window.ResizeMode` | `OverlappedPresenter.IsResizable = false` | Via presenter |

**Example: Borderless Window**:
```csharp
// WPF
<Window WindowStyle="None" AllowsTransparency="True" Background="Transparent">

// WinUI 3
public MainWindow()
{
    InitializeComponent();

    var appWindow = AppWindow;
    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
    appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

    var presenter = OverlappedPresenter.CreateForContextMenu();
    presenter.IsAlwaysOnTop = true;
    presenter.IsResizable = false;
    appWindow.SetPresenter(presenter);
}
```

### Window Dragging

| WPF | WinUI 3 |
|-----|---------|
| `Window.DragMove()` | *(Not available, implement manually with PointerPressed/Moved)* |

```csharp
// WinUI 3 drag-to-move
private void Border_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
    {
        AppWindow.Move(GetCursorPosition()); // Simplified, need actual drag logic
    }
}
```

### Multi-Monitor Support

| WPF | WinUI 3 |
|-----|---------|
| `System.Windows.Forms.Screen` class | `Microsoft.UI.Windowing.DisplayArea` |
| `SystemParameters.WorkArea` | `DisplayArea.GetFromWindowId().WorkArea` |

```csharp
// WPF
var workArea = SystemParameters.WorkArea;

// WinUI 3
var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
var workArea = displayArea.WorkArea; // RectInt32
```

---

## 11. Resources & Theming

### ResourceDictionary

| WPF | WinUI 3 | Notes |
|-----|---------|-------|
| `<ResourceDictionary>` | `<ResourceDictionary>` | Same |
| `<ResourceDictionary.MergedDictionaries>` | `<ResourceDictionary.MergedDictionaries>` | Same |
| `Source="/Styles/Colors.xaml"` | `Source="/Styles/Colors.xaml"` | Same path syntax |

### Theme Dictionaries (Light/Dark)

**WinUI 3 Only**:
```xml
<ResourceDictionary>
    <ResourceDictionary.ThemeDictionaries>
        <ResourceDictionary x:Key="Light">
            <SolidColorBrush x:Key="CustomBrush" Color="#FFFFFF"/>
        </ResourceDictionary>
        <ResourceDictionary x:Key="Dark">
            <SolidColorBrush x:Key="CustomBrush" Color="#000000"/>
        </ResourceDictionary>
    </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```

### Dynamic Resource vs ThemeResource

| WPF | WinUI 3 |
|-----|---------|
| `{StaticResource Key}` | `{StaticResource Key}` *(same)* |
| `{DynamicResource Key}` | `{ThemeResource Key}` *(responds to theme changes)* |

**Example**:
```xml
<!-- WPF -->
<Button Background="{DynamicResource AccentBrush}"/>

<!-- WinUI 3 -->
<Button Background="{ThemeResource AccentBrush}"/>
```

---

## 12. Icons & Fonts

### Segoe MDL2 Assets → Segoe Fluent Icons

**WPF** uses Segoe MDL2 Assets (older icon font):
```xml
<TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE720;"/>
```

**WinUI 3** uses Segoe Fluent Icons (newer, but codes may differ):
```xml
<FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE720;"/>
```

**Common Icon Mappings**:

| Icon | WPF (MDL2) | WinUI 3 (Fluent) | Notes |
|------|------------|------------------|-------|
| Microphone | `\uE720` | `\uE720` | Same |
| Microphone Off | `\uE74F` | `\uE74F` | Same |
| Volume | `\uE767` | `\uE767` | Same |
| Settings | `\uE713` | `\uE713` | Same |
| Pin | `\uE718` | `\uE718` | Same |
| Call | `\uE8BD` | `\uE8BD` | Same (phone icon) |

**Note**: Most MDL2 codes work in Fluent Icons, but verify for new/changed glyphs.

**SymbolIcon** (WinUI 3 shortcut):
```xml
<!-- Instead of -->
<FontIcon Glyph="&#xE72D;"/>

<!-- Use -->
<SymbolIcon Symbol="Setting"/>
```

### Icon in Code-Behind

```csharp
// WPF
var icon = new Icon("icon.ico");

// WinUI 3
var icon = Icon.FromHandle(new Bitmap("icon.ico").GetHicon());
// Or use BitmapImage for Image control
```

---

## 13. Common Gotchas

### 1. x:Bind Default Mode is OneTime

```xml
<!-- WPF: Default is OneWay -->
<TextBlock Text="{Binding Name}"/>

<!-- WinUI 3: Must specify OneWay for reactive updates -->
<TextBlock Text="{x:Bind Name, Mode=OneWay}"/>
```

### 2. DataTemplate Requires x:DataType

```xml
<!-- WPF: Works without DataType -->
<DataTemplate>
    <TextBlock Text="{Binding Name}"/>
</DataTemplate>

<!-- WinUI 3: Must specify x:DataType for x:Bind -->
<DataTemplate x:DataType="viewmodels:MicrophoneViewModel">
    <TextBlock Text="{x:Bind Name, Mode=OneWay}"/>
</DataTemplate>
```

### 3. No Direct Equivalent for Some WPF Controls

- **DataGrid**: Use `CommunityToolkit.WinUI.Controls.DataGrid` package
- **WrapPanel**: Use `ItemsRepeater` with `UniformGridLayout`
- **DockPanel**: Use `Grid` with row/column spans
- **Expander**: Use `Expander` from `CommunityToolkit.WinUI`

### 4. Effects Are Different

**WPF**:
```xml
<Border.Effect>
    <DropShadowEffect BlurRadius="15" ShadowDepth="2" Opacity="0.5"/>
</Border.Effect>
```

**WinUI 3**:
```xml
<Border.Shadow>
    <ThemeShadow />
</Border.Shadow>
```

### 5. ToolTip Syntax

**WPF**:
```xml
<Button ToolTip="Click me"/>
```

**WinUI 3**:
```xml
<Button ToolTipService.ToolTip="Click me"/>
```

### 6. ContextMenu → MenuFlyout

**WPF**:
```xml
<Button.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Copy"/>
    </ContextMenu>
</Button.ContextMenu>
```

**WinUI 3**:
```xml
<Button>
    <Button.Flyout>
        <MenuFlyout>
            <MenuFlyoutItem Text="Copy"/>
        </MenuFlyout>
    </Button.Flyout>
</Button>

<!-- Or use right-click via ContextFlyout -->
<Button.ContextFlyout>
    <MenuFlyout>
        <MenuFlyoutItem Text="Copy"/>
    </MenuFlyout>
</Button.ContextFlyout>
```

### 7. Visibility Enum Values Are Same

```csharp
// Both WPF and WinUI 3
Visibility.Visible
Visibility.Collapsed
// (WPF also has Visibility.Hidden, WinUI 3 does not)
```

### 8. Color Syntax

**WPF**:
```xml
<SolidColorBrush Color="#FF0078D4"/>
```

**WinUI 3**: Same, but prefer `Color.FromArgb()` in code:
```csharp
var color = Microsoft.UI.Colors.Blue; // Predefined
var custom = Color.FromArgb(255, 0, 120, 212);
```

### 9. Attached Properties Syntax

**Same in both**:
```xml
<Button Grid.Row="1" Grid.Column="2"/>
<TextBlock Canvas.Left="10" Canvas.Top="20"/>
```

### 10. No Application.DoEvents()

**WPF**:
```csharp
Application.Current.Dispatcher.Invoke(() => {}, DispatcherPriority.Background);
// Or use DoEvents workaround
```

**WinUI 3**: No equivalent. Use `await Task.Yield()` or proper async patterns.

---

## Quick Reference Table

| Feature | WPF | WinUI 3 |
|---------|-----|---------|
| Namespace prefix | `clr-namespace:` | `using:` |
| Binding | `{Binding}` | `{x:Bind Mode=OneWay}` |
| Dispatcher | `Dispatcher.BeginInvoke()` | `DispatcherQueue.TryEnqueue()` |
| Timer | `DispatcherTimer` | `DispatcherQueueTimer` |
| Icon font | Segoe MDL2 Assets | Segoe Fluent Icons |
| Triggers | `<Trigger>` | `<VisualState>` |
| ToolTip | `ToolTip="text"` | `ToolTipService.ToolTip="text"` |
| Context Menu | `<ContextMenu>` | `<MenuFlyout>` via `ContextFlyout` |
| Window size | `Width="400"` | `AppWindow.Resize()` |
| Theme resource | `{DynamicResource}` | `{ThemeResource}` |
| Drop shadow | `<DropShadowEffect>` | `<ThemeShadow>` |

---

## Migration Workflow

1. **Copy XAML**: Start with WPF XAML as baseline
2. **Update namespaces**: `clr-namespace:` → `using:`
3. **Replace Binding**: `{Binding}` → `{x:Bind Mode=OneWay}`
4. **Add x:DataType**: In DataTemplates
5. **Convert Triggers**: To VisualStates (biggest effort)
6. **Update controls**: ListBox → ListView, ContextMenu → MenuFlyout, etc.
7. **Fix code-behind**: Dispatcher APIs, Window APIs
8. **Test incrementally**: Build after each section

---

## Resources

- **WinUI 3 Documentation**: https://learn.microsoft.com/en-us/windows/apps/winui/winui3/
- **Windows App SDK**: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- **Segoe Fluent Icons List**: https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font
- **CommunityToolkit.WinUI**: https://learn.microsoft.com/en-us/windows/communitytoolkit/

---

**Document Version**: 1.0
**Last Updated**: 2026-01-04
**Reviewed By**: Migration Agent
