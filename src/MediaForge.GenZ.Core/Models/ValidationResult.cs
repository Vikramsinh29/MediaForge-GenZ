namespace MediaForge.GenZ.Core.Models;

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, []);

    public static ValidationResult Failure(params string[] errors) =>
        new(false, errors);
}
