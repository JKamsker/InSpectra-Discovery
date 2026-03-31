namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Text.Json;

internal static class HookCaptureDeserializer
{
    public static HookCaptureResult? Deserialize(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HookCaptureResult>(json);
        }
        catch
        {
            return null;
        }
    }
}


