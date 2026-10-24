using System.Text.Json;

namespace SagaOutBoxCleanArch.Application.Messaging;

public static class MessageSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Deserializes a message body, throwing <see cref="JsonException"/> (a poison message) when it is empty.</summary>
    public static T Deserialize<T>(string body, string description) where T : class =>
        JsonSerializer.Deserialize<T>(body, Options) ?? throw new JsonException($"Empty {description}.");
}
