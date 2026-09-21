using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RomM.Client;

/// <summary>
/// Reads a RomM timestamp that carries no zone as the UTC it is.
/// </summary>
/// <remarks>
/// <b>RomM serialises its datetimes without an offset and stores UTC</b>, and
/// <c>System.Text.Json</c> reads a zone-less value as <b>local</b> time. So a plain
/// <c>DateTimeOffset</c> property is wrong by the machine's own offset, silently, and reads as
/// right on a UTC machine. Driven against the live instance: a play session the agent had just
/// read back came out four hours ahead of the same run's <c>Date</c> header, which put a
/// finished session in the future.
/// <para>
/// The same reasoning as <c>RomRow.UpdatedAtUtc</c>, which does it by hand because its raw
/// field is a string. This is the form for a field that should simply be an instant.
/// </para>
/// <para>
/// A value that does carry an offset is honoured, so this is safe on any field whether or not
/// the server names a zone.
/// </para>
/// </remarks>
public sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();

        // JsonException rather than the FormatException Parse throws, because
        // RomMConnection.ReadAsync turns only that one into RomMApiException and anything else
        // leaves the process on an unhandled exception.
        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var moment)
            ? moment
            : throw new JsonException($"'{raw}' is not a timestamp this client can read.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime());
}
