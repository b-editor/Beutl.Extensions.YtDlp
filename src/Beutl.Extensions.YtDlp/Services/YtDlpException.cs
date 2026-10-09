namespace Beutl.Extensions.YtDlp.Services;

public sealed class YtDlpException(string message, int exitCode, string standardError) : Exception(message)
{
    public int ExitCode { get; } = exitCode;

    public string StandardError { get; } = standardError;
}
