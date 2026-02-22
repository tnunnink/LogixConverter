namespace LogixConverter.Abstractions;

/// <summary>
/// Represents a client interface for interacting with a design project in the application.
/// Provides functionality to open and manage design projects.
/// </summary>
public interface ILogixFileConverter
{
    /// <summary>
    /// Converts a file from one format to another and saves the converted file.
    /// </summary>
    /// <param name="filePath">The path to the source file to be converted.</param>
    /// <param name="savePath">The path where the converted file will be saved.</param>
    /// <param name="options">Optional settings for the conversion process, such as overwrite or detailed mode.</param>
    /// <param name="token">A token to observe while waiting for the task to complete, allowing cancellation.</param>
    /// <returns>A <see cref="ConversionResult"/> object that contains details about the conversion operation, including the source and output paths, warnings, and the type of conversion performed.</returns>
    Task<ConversionResult> ConvertAsync(
        string filePath,
        string savePath,
        ConversionOptions? options = null,
        CancellationToken token = default
    );
}