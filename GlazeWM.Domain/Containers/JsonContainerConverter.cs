using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Monitors;
using GlazeWM.Domain.Windows;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Serialization;
using GlazeWM.Infrastructure.WindowsApi;

namespace GlazeWM.Domain.Containers
{
  public class JsonContainerConverter : JsonConverter<Container>
  {
    public override bool CanConvert(Type typeToConvert)
    {
      return typeof(Container).IsAssignableFrom(typeToConvert);
    }

    public override Container Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options)
    {
      using var jsonDocument = JsonDocument.ParseValue(ref reader);

      return DeserializeContainerJson(jsonDocument.RootElement, options);
    }

    private static Container DeserializeContainerJson(
      JsonElement jsonObject,
      JsonSerializerOptions options,
      Container parent = null)
    {
      // Get the type of container (eg. "Workspace", "MinimizedWindow").
      var typeDiscriminator = jsonObject.GetProperty("__type").ToString();

      Container newContainer = typeDiscriminator switch
      {
        "RootContainer" => new RootContainer(),
        "Monitor" => new Monitor(
          jsonObject.GetProperty("DeviceName").GetString(),
          jsonObject.GetProperty("Width").GetInt32(),
          jsonObject.GetProperty("Height").GetInt32(),
          jsonObject.GetProperty("X").GetInt32(),
          jsonObject.GetProperty("Y").GetInt32()
        ),
        "Workspace" => new Workspace(
          jsonObject.GetProperty("Name").GetString(),
          jsonObject.GetProperty("Layout").Deserialize<Layout>()
        ),
        "SplitContainer" => new SplitContainer
        {
          Layout = jsonObject.GetProperty("Layout").Deserialize<Layout>(),
          SizePercentage = jsonObject.GetProperty("SizePercentage").GetDouble()
        },
        "MinimizedWindow" => new MinimizedWindow(
          // TODO: Handle `IntPtr` for 32-bit processes.
          new IntPtr(Convert.ToInt64(jsonObject.GetProperty("Handle").GetString(), 16)),
          jsonObject.GetProperty("FloatingPlacement").Deserialize<Rect>(),
          jsonObject.GetProperty("BorderDelta").Deserialize<RectDelta>(),
          jsonObject.GetEnumProperty<WindowType>("PreviousState", options)
        ),
        "FloatingWindow" => new FloatingWindow(
          // TODO: Handle `IntPtr` for 32-bit processes.
          new IntPtr(Convert.ToInt64(jsonObject.GetProperty("Handle").GetString(), 16)),
          jsonObject.GetProperty("FloatingPlacement").Deserialize<Rect>(),
          jsonObject.GetProperty("BorderDelta").Deserialize<RectDelta>()
        ),
        "TilingWindow" => new TilingWindow(
          // TODO: Handle `IntPtr` for 32-bit processes.
          new IntPtr(Convert.ToInt64(jsonObject.GetProperty("Handle").GetString(), 16)),
          jsonObject.GetProperty("FloatingPlacement").Deserialize<Rect>(),
          jsonObject.GetProperty("BorderDelta").Deserialize<RectDelta>(),
          jsonObject.GetProperty("SizePercentage").GetDouble()
        ),
        _ => throw new ArgumentException(null, nameof(jsonObject)),
      };

      newContainer.Parent = parent;

      var children = jsonObject.GetProperty("Children").EnumerateArray();
      newContainer.Children = children
        .Select((child) => DeserializeContainerJson(child, options, newContainer))
        .ToList();

      var focusIndices =
        children.Select(child => child.GetProperty("FocusIndex").GetInt32());

      // Map focus index to the corresponding child container.
      newContainer.ChildFocusOrder = focusIndices
        .Select(focusIndex => newContainer.Children[focusIndex])
        .ToList();

      return newContainer;
    }

    public override void Write(
      Utf8JsonWriter writer,
      Container value,
      JsonSerializerOptions options)
    {
      writer.WriteStartObject();
      writer.WriteNumber("X", value.X);
      writer.WriteNumber("Y", value.Y);
      writer.WriteNumber("Width", value.Width);
      writer.WriteNumber("Height", value.Height);
      writer.WriteString("__type", value.GetType().Name);

      // Handle focus index for root container.
      var focusIndex = value is RootContainer ? 0 : value.FocusIndex;
      writer.WriteNumber("FocusIndex", focusIndex);

      WriteContainerValue(writer, value);

      // Recursively serialize child containers.
      writer.WriteStartArray("Children");
      foreach (var child in value.Children)
        Write(writer, child, options);

      writer.WriteEndArray();
      writer.WriteEndObject();
    }

    private static void WriteContainerValue(
      Utf8JsonWriter writer,
      Container value)
    {
      switch (value)
      {
        case Monitor:
          var monitor = value as Monitor;
          writer.WriteString("DeviceName", monitor.DeviceName);
          return;
        case Workspace:
          var workspace = value as Workspace;
          writer.WriteString("Name", workspace.Name);
          return;
        case SplitContainer:
          var splitContainer = value as SplitContainer;
          writer.WriteString("Layout", splitContainer.Layout.ToString());
          writer.WriteNumber("SizePercentage", splitContainer.SizePercentage);
          return;
        case MinimizedWindow:
          var minimizedWindow = value as MinimizedWindow;
          writer.WriteNumber(
            "Handle",
            minimizedWindow.Handle.ToInt64()
          );
          writer.WritePropertyName("FloatingPlacement");
          JsonSerializer.Serialize(writer, minimizedWindow.FloatingPlacement);
          writer.WritePropertyName("BorderDelta");
          JsonSerializer.Serialize(writer, minimizedWindow.BorderDelta);
          writer.WriteString("PreviousState", minimizedWindow.PreviousState.ToString());
          return;
        case FloatingWindow:
          var floatingWindow = value as FloatingWindow;
          writer.WriteNumber(
            "Handle",
            floatingWindow.Handle.ToInt64()
          );
          writer.WritePropertyName("FloatingPlacement");
          JsonSerializer.Serialize(writer, floatingWindow.FloatingPlacement);
          writer.WritePropertyName("BorderDelta");
          JsonSerializer.Serialize(writer, floatingWindow.BorderDelta);
          return;
        case TilingWindow:
          var tilingWindow = value as TilingWindow;
          writer.WriteNumber(
            "Handle",
            tilingWindow.Handle.ToInt64()
          );
          writer.WritePropertyName("FloatingPlacement");
          JsonSerializer.Serialize(writer, tilingWindow.FloatingPlacement);
          writer.WritePropertyName("BorderDelta");
          JsonSerializer.Serialize(writer, tilingWindow.BorderDelta);
          writer.WriteNumber("SizePercentage", tilingWindow.SizePercentage);
          return;
        default:
          return;
      }
    }
  }
}
