namespace MediaForge.Core.Models;

public sealed class ConversionResult
{
    public bool Success { get; set; }

    public string? OutputFile { get; set; }

    public int ExitCode { get; set; }

    public string? ErrorMessage { get; set; }
}