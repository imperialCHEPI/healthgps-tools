using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using HealthGps.R;

namespace HealthGps.ModelFit
{
    public class Array2dJsonConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsGenericType &&
                   typeToConvert.GetGenericTypeDefinition() == typeof(Array2D<>);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type elementType = typeToConvert.GetGenericArguments()[0];

            JsonConverter converter = (JsonConverter)Activator.CreateInstance(
                typeof(JsonValueConverterArray2d<>).MakeGenericType(new Type[] { elementType }),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                args: new object[] { options },
                culture: null);

            return converter;
        }

        private class JsonValueConverterArray2d<TValue> : JsonConverter<Array2D<TValue>>
            where TValue : struct, IComparable, IComparable<TValue>, IEquatable<TValue>, IConvertible
        {
            private readonly JsonConverter<TValue[]> converter;
            private readonly Type valueType;

            public JsonValueConverterArray2d(JsonSerializerOptions options)
            {
                // Use the existing converter if available, cache value type
                converter = (JsonConverter<TValue[]>)options.GetConverter(typeof(TValue[]));
                valueType = typeof(TValue[]);
            }

            public override Array2D<TValue> Read(
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                int rows = 0;
                int cols = 0;
                TValue[] data = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("Expected PropertyName token");
                    }

                    var propName = reader.GetString();
                    reader.Read();

                    if (propName.Equals("rows", StringComparison.OrdinalIgnoreCase))
                    {
                        rows = reader.GetInt32();
                    }
                    else if (propName.Equals("cols", StringComparison.OrdinalIgnoreCase))
                    {
                        cols = reader.GetInt32();
                    }
                    else if (propName.Equals("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (converter != null)
                        {
                            data = converter.Read(ref reader, valueType, options);
                        }
                        else
                        {
                            data = JsonSerializer.Deserialize<TValue[]>(ref reader, options);
                        }
                    }
                    else
                    {
                        throw new JsonException($"Expected PropertyName token: {propName}");
                    }
                }

                return new Array2D<TValue>(rows, cols, data);
            }

            public override void Write(
                Utf8JsonWriter writer,
                Array2D<TValue> value,
                JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteNumber("rows", value.Rows);
                writer.WriteNumber("cols", value.Columns);
                writer.WritePropertyName("data");
                if (converter != null)
                {
                    converter.Write(writer, value.ToArray1D(), options);
                }
                else
                {
                    JsonSerializer.Serialize(writer, value.ToArray1D(), options);
                }

                writer.WriteEndObject();
            }
        }
    }
}
