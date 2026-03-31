using System.Text.Json;

internal static class CaptureFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IgnoreNullValues = true,
    };

    public static void Write(string path, CaptureResult result, bool overwrite = true)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(result, JsonOptions);
            using var stream = new FileStream(
                path,
                overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(json);
        }
        catch
        {
            // Best-effort: if we can't write, the main tool will see a missing file.
        }
    }

    public static void WriteError(string path, string status, string error, bool overwrite = true)
    {
        Write(path, new CaptureResult
        {
            Status = status,
            Error = error,
        }, overwrite);
    }
}
