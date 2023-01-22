using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace GlazeWM.Infrastructure.Serialization
{
  public class YamlService
  {
    private readonly JsonService _jsonService;
    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder().Build();

    public YamlService(JsonService jsonService)
    {
      _jsonService = jsonService;
    }

    /// <summary>
    /// The YAML deserialization library doesn't have support for polymorphic objects. Because of
    /// this, the YAML is first converted into JSON and then deserialized via `System.Text.Json`.
    /// </summary>
    public T Deserialize<T>(string input, List<JsonConverter> converters)
    {
      // Deserializes YAML into key-value pairs (ie. not an object of type `T`). Merging parser is
      // used to enable the use of merge keys.
      var reader = new MergingParser(new Parser(new StringReader(input)));
      var yamlObject = _yamlDeserializer.Deserialize(reader);

      // Convert key-value pairs into a JSON string.
      var jsonString = _jsonService.Serialize(yamlObject, new List<JsonConverter>());

      return _jsonService.Deserialize<T>(jsonString, converters);
    }
  }
}
