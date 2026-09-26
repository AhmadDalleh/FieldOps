using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldOps.Api.Tests.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
