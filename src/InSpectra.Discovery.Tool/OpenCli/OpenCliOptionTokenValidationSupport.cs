using System.Text.Json.Nodes;

internal static class OpenCliOptionTokenValidationSupport
{
    public static IEnumerable<string> EnumerateOptionTokens(JsonObject optionNode)
    {
        var name = OpenCliValidationSupport.GetString(optionNode["name"]);
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name.Trim();
        }

        if (optionNode["aliases"] is not JsonArray aliases)
        {
            yield break;
        }

        foreach (var alias in aliases)
        {
            var aliasValue = OpenCliValidationSupport.GetString(alias);
            if (!string.IsNullOrWhiteSpace(aliasValue))
            {
                yield return aliasValue.Trim();
            }
        }
    }
}
