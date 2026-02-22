namespace LogixConverter.Abstractions;

/// <summary>
/// Represents a set of options that can influence the behavior of a file conversion process.
/// </summary>
public sealed record ConversionOptions(
    bool Overwrite = true,
    bool Detailed = true
);