using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GlazeWM.Domain.UserConfigs
{
  public class BarComponentConfigConverter : JsonConverter
  {
    public override bool CanConvert(Type objectType)
    {
      return objectType.IsAssignableFrom(typeof(BarComponentConfig));
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
      var jObject = JObject.Load(reader);

      // Get the type of workspace component config.
      var type = jObject["type"].Value<string>();

      object target = type switch
      {
        "workspaces" => new WorkspacesComponentConfig(),
        "clock" => new ClockComponentConfig(),
        _ => throw new ArgumentException($"Invalid workspace type '{type}'."),
      };

      serializer.Populate(jObject.CreateReader(), target);

      return target;
    }

    /// <summary>
    /// Serializing is not needed, so it's fine to leave it unimplemented.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      throw new NotImplementedException();
    }
  }
}
