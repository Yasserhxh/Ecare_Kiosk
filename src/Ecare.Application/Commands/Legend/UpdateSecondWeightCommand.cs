using Ecare.Application.Dtos;
using Ecare.Shared;
using MediatR;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ecare.Application.Commands.Legend;

public sealed record UpdateSecondWeightCommand(
    [property: JsonConverter(typeof(FlexibleStringJsonConverter))]
    string RfidCard,
    string Matricule,
    int DeuxiemePoid,
    int? LegendId = null,
    string? DeviceId = null
) : IRequest<Result<UpdateSecondWeightResult>>;

public sealed class UpdateSecondWeightResult
{
    public bool Success { get; init; }
    public BonDeLivraisonDto? BonDeLivraison { get; init; }
}

public sealed class FlexibleStringJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetInt64().ToString(),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
