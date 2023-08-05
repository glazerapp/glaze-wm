﻿# Adding Components

Adding a new component is relatively trivial and involves a few simple steps; though requires some upfront plumbing. This document should hopefully help make this process easier.

## Adding a Config

First add a config to the `GlazeWM.Domain.UserConfigs` namespace; here is an example:

```csharp
public class CpuComponentConfig : BarComponentConfig
{
    /// <summary>
    /// Label assigned to the CPU component.
    /// </summary>
    public string Label { get; set; } = "CPU: {percent_usage}%";
}
```

Once this is done, you should register this config in `BarComponentConfigConverter.Read`:

```csharp
"cpu" =>
  JsonSerializer.Deserialize<CpuComponentConfig>(
    jsonObject.RootElement.ToString(),
    options
  )
```

This will map directly to the config using the YAML naming rules, i.e.

```yaml
components_right:
  - type: "cpu"
    label: "CPU: {percent_usage}%"
```

Each capital letter is prefixed by a space delimited by `_`.

## Adding a Service [Optional]

In some cases to add a component, you might want to register a `'Service'`; this will allow your logic to be reused across multiple locations in the program.

To do so, first create the raw service in `GlazeWM.Infrastructure`.

```csharp
/// <summary>
/// Provides access to current CPU statistics.
/// </summary>
public class CpuStatsService
{ ...  }
```

And register it in the DI container in `GlazeWM.Infrastructure.DependencyInjection`:

```csharp
services.AddSingleton<CpuStatsService>();
```

## Adding a ViewModel

This contains the main logic used to fetch the data to be displayed in the UI.
You add this file to `GlazeWM.Bar.Components`.

Example:

```csharp
public class CpuComponentViewModel : ComponentViewModel
{
  public CpuComponentViewModel(
    BarViewModel parentViewModel,
    CpuComponentConfig config) : base(parentViewModel, config)
  {
    _config = config;
    _cpuStatsService = ServiceLocator.GetRequiredService<CpuStatsService>();

    Observable.Timer(
      TimeSpan.Zero,
      TimeSpan.FromMilliseconds(_config.RefreshIntervalMs)
    )
      .TakeUntil(_parentViewModel.WindowClosing)
      .Subscribe((_) => Label = CreateLabel());
  }
}
```

Note the line `.TakeUntil(_parentViewModel.WindowClosing)`. This is necessary to correctly clean up after config reloads; otherwise your component will remain alive; wasting CPU and RAM.

Then register your ViewModel to `BarViewModel.CreateComponentViewModels`

```csharp
CpuComponentConfig cpupc => new CpuComponentViewModel(this, cpupc),
```

## Adding a Component XAML

Each individual component has its own custom XAML WPF UserControl.
Most of these don't require much customization; so it's usually easier to just copy an existing component; for example.

### Copy Existing Component

CTRL+C & CTRL+V existing Xaml file

![Duplicate XAML](./docs/images/duplicate_xaml.png)

### Fix Class Name

Fix the class name to correspond to your new class in the XAML, i.e.

```xaml
<UserControl
  x:Class="GlazeWM.Bar.Components.CpuComponent"
```

(first line)

And corresponding `xaml.cs` file.

```csharp
public partial class CpuComponent : UserControl
{
  public CpuComponent() => InitializeComponent();
}
```

### Fix Item Name

Change following line in the XAML

```xaml
<!-- Or whichever property is here -->
Text="{Binding TilingDirectionString}" />
```

To match your property from the `ViewModel`, i.e. `FormattedText`, so the XAML reads.

```xaml
Text="{Binding FormattedText}" />
```

## Register Component XAML

Add your component to `ComponentPortal.xaml` beside all the other components.

```xaml
<DataTemplate DataType="{x:Type components:CpuComponentViewModel}">
  <components:CpuComponent Padding="{Binding Padding}" Background="{Binding Background}" />
</DataTemplate>
```

After doing this, add the component to your config, see if it works.

![Working CPU Indicator](./docs/images/working_cpu_indicator.png)
