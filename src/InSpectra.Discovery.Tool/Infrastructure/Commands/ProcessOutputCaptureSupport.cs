namespace InSpectra.Discovery.Tool.Infrastructure.Commands;

using System.Text;

internal static class ProcessOutputCaptureSupport
{
    internal const int MaxCapturedOutputCharacters = 4 * 1024 * 1024;
    private const string TruncationMarker = "[inspectra-output-truncated]";

    public static string BuildOutputLimitExceededMessage()
        => $"Command emitted more than {MaxCapturedOutputCharacters} characters of console output.";

    public static LimitedOutputBuffer CreateBuffer()
        => new(MaxCapturedOutputCharacters, TruncationMarker);

    internal sealed class LimitedOutputBuffer(int maxCharacters, string truncationMarker)
    {
        private readonly StringBuilder _builder = new();

        public bool LimitExceeded { get; private set; }

        public void Append(char[] chunk, int readCount)
        {
            if (LimitExceeded || readCount <= 0)
            {
                return;
            }

            var remainingCapacity = maxCharacters - _builder.Length;
            if (remainingCapacity <= 0)
            {
                AppendMarker();
                return;
            }

            var countToAppend = Math.Min(readCount, remainingCapacity);
            _builder.Append(chunk, 0, countToAppend);
            if (countToAppend < readCount)
            {
                AppendMarker();
            }
        }

        public override string ToString()
            => _builder.ToString();

        private void AppendMarker()
        {
            if (LimitExceeded)
            {
                return;
            }

            LimitExceeded = true;
            if (_builder.Length > 0 && _builder[^1] != '\n')
            {
                _builder.AppendLine();
            }

            _builder.Append(truncationMarker);
        }
    }
}
