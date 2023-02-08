using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthGPS.Tools.Output;

public class FactorMomentJsonConverter : JsonConverter<FactorMoment>
{
    public override FactorMoment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, FactorMoment value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("male");
        var males = value.Males.Select(s => s.Value).ToList();
        JsonSerializer.Serialize(writer, males, options);

        writer.WritePropertyName("female");
        var females = value.Females.Select(s => s.Value).ToList();
        JsonSerializer.Serialize(writer, females, options);
        writer.WriteEndObject();
    }
}