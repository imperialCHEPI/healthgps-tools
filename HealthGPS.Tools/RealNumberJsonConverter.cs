using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthGPS.Tools;

public class RealNumberJsonConverter : JsonConverter<double>
{
    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value);
    }


    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetDouble(out double value))
        {
            return value;
        }

        return double.NaN;
    }
}