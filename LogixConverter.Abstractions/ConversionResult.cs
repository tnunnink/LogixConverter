namespace LogixConverter.Abstractions;

/// <summary>
/// Represents the result of a file conversion operation.
/// </summary>
/// <remarks>
/// Instances of this class encapsulate details about the success or failure of a file conversion,
/// the source and destination file paths, the duration of the conversion process, the timestamp
/// when the conversion occurred, and any errors produced during the conversion.
/// This class is immutable and thread-safe.
/// </remarks>
public sealed record ConversionResult(
    bool Success,
    string SourceFile,
    string DesitnationFile,
    TimeSpan Duration,
    DateTime TimeStamp,
    string? Error = null
)
{
    /// <summary>
    /// Creates a successful ConversionResult instance indicating a passed file conversion operation.
    /// </summary>
    /// <param name="source">The file path of the source file that was converted.</param>
    /// <param name="desitnation">The file path of the destination file where the conversion output was saved.</param>
    /// <param name="duration">The time taken to complete the conversion process.</param>
    /// <returns>A ConversionResult object representing a successful file conversion operation.</returns>
    public static ConversionResult Passed(string source, string desitnation, TimeSpan duration)
    {
        return new ConversionResult(true, source, desitnation, duration, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a failed ConversionResult instance indicating an unsuccessful file conversion operation.
    /// </summary>
    /// <param name="source">The file path of the source file that was attempted to be converted.</param>
    /// <param name="desitnation">The file path of the destination file where the failed conversion output was intended to be saved.</param>
    /// <param name="duration">The time elapsed during the attempted conversion process.</param>
    /// <param name="error">A description of the error or failure reason that occurred during the conversion process.</param>
    /// <returns>A ConversionResult object representing a failed file conversion operation.</returns>
    public static ConversionResult Failed(string source, string desitnation, TimeSpan duration, string error)
    {
        return new ConversionResult(false, source, desitnation, duration, DateTime.UtcNow, error);
    }
};