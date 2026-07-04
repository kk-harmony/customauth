using System.Text.Json;

namespace CustomOAuthServer.Application.OAuth;

public static class ClientAudienceProperties
{
    public const string AllowedAudiencesKey = "allowed_audiences";

    public static IReadOnlyList<string> GetAllowedAudiences(IDictionary<string, JsonElement> properties)
    {
        if (!properties.TryGetValue(AllowedAudiencesKey, out var element))
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
    }

    public static void SetAllowedAudiences(
        IDictionary<string, JsonElement> properties,
        IReadOnlyList<string>? audiences)
    {
        if (audiences is null || audiences.Count == 0)
        {
            properties.Remove(AllowedAudiencesKey);
            return;
        }

        properties[AllowedAudiencesKey] = JsonSerializer.SerializeToElement(audiences);
    }
}
