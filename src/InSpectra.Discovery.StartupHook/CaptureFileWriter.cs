using System.Text.Json;

internal static class CaptureFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(string path, CaptureResult result)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(result, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Best-effort: if we can't write, the main tool will see a missing file.
        }
    }

    public static void WriteError(string path, string status, string error)
    {
        Write(path, new CaptureResult
        {
            Status = status,
            Error = error,
        });
    }
}
