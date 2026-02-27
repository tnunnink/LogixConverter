using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using LogixConverter.Abstractions;

namespace LogixConverter.LogixSdk;

/// <summary>
/// An implementation of the <see cref="ILogixFileConverter"/> that wraps the Rockwell Logix Designer SDK using
/// reflection so that we don't need to rely on local nuget feed or software installations. This implementation
/// loads the Rockwell assembly provided in the nuget package on the local machine or at the specified override path.
/// Once loaded, it will use the open project and save as methods to convert a file from ACD > L5X or L5X > ACD
/// depending on the extensions of the provided files.
/// </summary>
public class LogixSdkConverter(string? packageLocation = null) : ILogixFileConverter
{
    private const string DefaultNuGetFolder = @"C:\Users\Public\Documents\Studio 5000\Logix Designer SDK\dotnet";
    private const string TargetAssembly = "RockwellAutomation.LogixDesigner.LogixProject.CSClient.dll";
    private const string EntryType = "RockwellAutomation.LogixDesigner.LogixProject";
    private const string OpenProjectMethod = "OpenLogixProjectAsync";
    private const string SaveProjectMethod = "SaveAsAsync";

    /// <inheritdoc />
    public async Task<ConversionResult> ConvertAsync(
        string filePath,
        string savePath,
        ConversionOptions? options = null,
        CancellationToken token = default)
    {
        options ??= new ConversionOptions();
        var source = ResolveSourceFile(filePath, out var tempFile);
        var destination = ResolveDesitnationFile(savePath);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var logixProject = await OpenProjectAsync(tempFile, token);
            await SaveProjectAsync(logixProject, destination, options, token);
            stopwatch.Stop();
            return ConversionResult.Passed(source, destination, stopwatch.Elapsed);
        }
        catch (Exception e)
        {
            stopwatch.Stop();
            return ConversionResult.Failed(source, destination, stopwatch.Elapsed, e.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Asynchronously opens a Logix project file for processing.
    /// </summary>
    /// <param name="filePath">The file path of the Logix project to be opened.</param>
    /// <param name="token">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An object representing the opened Logix project.</returns>
    private async Task<object> OpenProjectAsync(string filePath, CancellationToken token)
    {
        var logixAssembly = LoadSdkAssembly(packageLocation ?? DefaultNuGetFolder);
        var openMethod = GetOpenProjectMethodFromAssembly(logixAssembly);

        var openResult = openMethod.Invoke(null,
        [
            filePath,
            null, // operationEventHandler (can add later if we figure out what to do about logging)
            token
        ]);

        if (openResult is not Task openTask)
            throw new InvalidOperationException($"{OpenProjectMethod} did not return a Task.");

        await openTask.ConfigureAwait(false);

        var resultProperty = openResult.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
        var logixProject = resultProperty?.GetValue(openResult);

        if (logixProject is null)
            throw new InvalidOperationException($"Could not read Result from {OpenProjectMethod} Task.");

        return logixProject;
    }

    /// <summary>
    /// Saves a Logix project to the specified path with the given conversion options.
    /// </summary>
    /// <param name="logixProject">The project object representing the Logix file to be saved.</param>
    /// <param name="savePath">The path to save the converted Logix project file.</param>
    /// <param name="options">The conversion options that influence how the project is saved.</param>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous operation of saving the project.</returns>
    private static async Task SaveProjectAsync(object logixProject, string savePath, ConversionOptions options,
        CancellationToken token)
    {
        var projectType = logixProject.GetType();
        var saveAsMethod = GetSaveAsMethodFromType(projectType);

        var saveAsResult = saveAsMethod.Invoke(logixProject,
        [
            savePath,
            options.Overwrite,
            options.Detailed,
            token
        ]);

        if (saveAsResult is not Task saveAsTask)
            throw new InvalidOperationException($"{SaveProjectMethod} did not return a Task.");

        await saveAsTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves the "OpenLogixProjectAsync" method information from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the target entry type and method definition.</param>
    /// <returns>The method information for "OpenLogixProjectAsync".</returns>
    /// <exception cref="TypeLoadException">
    /// Thrown when the entry type "RockwellAutomation.LogixDesigner.LogixProject" cannot be found in the assembly.
    /// </exception>
    /// <exception cref="MissingMethodException">
    /// Thrown when the "OpenLogixProjectAsync" method cannot be found in the identified entry type.
    /// </exception>
    private static MethodInfo GetOpenProjectMethodFromAssembly(Assembly assembly)
    {
        var entryType = assembly.GetType(EntryType);

        if (entryType is null)
            throw new TypeLoadException($"Could not find entry type {EntryType} in assembly.");

        var openMethod = entryType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == OpenProjectMethod);

        if (openMethod is null)
            throw new MissingMethodException(entryType.FullName, OpenProjectMethod);

        return openMethod;
    }

    /// <summary>
    /// Retrieves the <c>SaveAsAsync</c> method from the specified project type.
    /// </summary>
    /// <param name="projectType">The type of the project object which contains the <c>SaveAsAsync</c> method.</param>
    /// <returns>The <see cref="MethodInfo"/> representing the <c>SaveAsAsync</c> method.</returns>
    /// <exception cref="MissingMethodException">Thrown when the <c>SaveAsAsync</c> method is not found in the specified project type.</exception>
    private static MethodInfo GetSaveAsMethodFromType(Type projectType)
    {
        var saveAsMethod = projectType.GetMethod(SaveProjectMethod, BindingFlags.Public | BindingFlags.Instance);

        if (saveAsMethod is null)
            throw new MissingMethodException(projectType.FullName, SaveProjectMethod);

        return saveAsMethod;
    }

    /// <summary>
    /// Loads the SDK assembly from the specified package location.
    /// </summary>
    /// <param name="packageLocation">The path to the directory containing the SDK package. If null, the default SDK location is used.</param>
    /// <returns>The loaded <see cref="Assembly"/> instance representing the Rockwell SDK.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the SDK package or assembly cannot be found in the specified location or default location.</exception>
    private static Assembly LoadSdkAssembly(string packageLocation)
    {
        if (!TryFindLatestPackage(packageLocation, out var packagePath))
            throw new FileNotFoundException(
                $"Rockwell SDK package '{packageLocation}' not found. Ensure SDK is installed or provide custom location.");

        var packageCache = ExtractToCache(packagePath);

        if (!TryFindAssemblyPath(packageCache, out var assemblyPath))
            throw new FileNotFoundException(
                "Could not find SDK assembly in extracted package", packageCache);

        return Assembly.LoadFile(assemblyPath);
    }

    /// <summary>
    /// Attempts to locate the path of the specified assembly within a given SDK package cache directory.
    /// </summary>
    /// <param name="packageCache">The directory path of the extracted SDK package cache to search for the assembly.</param>
    /// <param name="assemblyPath">
    /// When this method returns, contains the full file path of the assembly if found; otherwise, null.
    /// </param>
    /// <returns>
    /// <c>true</c> if the assembly is found in the specified package cache directory; otherwise, <c>false</c>.
    /// </returns>
    private static bool TryFindAssemblyPath(string packageCache, out string assemblyPath)
    {
        var targetAssembly = Directory.EnumerateFiles(
                packageCache,
                TargetAssembly,
                SearchOption.AllDirectories)
            .FirstOrDefault();

        if (targetAssembly is not null)
        {
            assemblyPath = targetAssembly;
            return true;
        }

        assemblyPath = null!;
        return false;
    }

    /// <summary>
    /// Attempts to find the latest package within the specified directory.
    /// </summary>
    /// <param name="packageDirectory">The path to the directory containing package files.</param>
    /// <param name="packagePath">
    /// When this method returns, contains the path to the latest package file
    /// if one is found; otherwise, null.
    /// </param>
    /// <returns>
    /// <c>true</c> if the latest package was successfully found; otherwise, <c>false</c>.
    /// </returns>
    private static bool TryFindLatestPackage(string packageDirectory, out string packagePath)
    {
        packagePath = null!;

        if (!Directory.Exists(packageDirectory))
            return false;

        var latest = Directory
            .EnumerateFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (latest is not null)
        {
            packagePath = latest;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the specified NuGet package file to a common cache directory for all users on the machine.
    /// Also moves the FSTP adapter executable to the same location as the DLL file since that is where it will look
    /// for the application when loading projects.
    /// </summary>
    private static string ExtractToCache(string packagePath)
    {
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LogixDesignerPackageCache");

        var folderName = Path.GetFileNameWithoutExtension(packagePath);
        var cacheLocation = Path.Combine(cacheRoot, folderName);

        if (!Directory.Exists(cacheLocation) || !Directory.EnumerateFileSystemEntries(cacheLocation).Any())
        {
            Directory.CreateDirectory(cacheLocation);
            ZipFile.ExtractToDirectory(packagePath, cacheLocation, overwriteFiles: true);
        }

        StageFtspAdapter(cacheLocation);
        return cacheLocation;
    }

    /// <summary>
    /// Ensures the presence of the FtspAdapter executable in the appropriate locations within the cache directory
    /// extracted from the specified package.
    /// </summary>
    private static void StageFtspAdapter(string cacheLocation)
    {
        // Build the path to the adapter exe which is in a known root directory.
        var adapterExecutable = Path.Combine(cacheLocation, "FtspAdapter", "FtspAdapter.exe");

        if (!File.Exists(adapterExecutable))
        {
            throw new FileNotFoundException(
                "FtspAdapter.exe was not found in extracted package contents. The Rockwell SDK requires it beside the SDK DLL.",
                adapterExecutable);
        }

        // Find all .netX.0 folders to copy the adapter app to since the SDK requires it in the same location as the DLL.
        var assemblyLocations = Directory.EnumerateDirectories(cacheLocation, "net*", SearchOption.AllDirectories);

        foreach (var location in assemblyLocations)
        {
            var destinationFile = Path.Combine(location, Path.GetFileName(adapterExecutable));
            File.Copy(adapterExecutable, destinationFile, overwrite: true);
        }
    }

    /// <summary>
    /// Resolves the source file path by verifying its existence, getting the absolute path,
    /// and creating a temporary file for conversion.
    /// </summary>
    /// <param name="filePath">The path to the source file to be resolved.</param>
    /// <param name="tempPath">
    /// When the method returns, contains the path to the temporary file created during the resolution process.
    /// </param>
    /// <returns>The resolved absolute path of the source file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified filePath does not exist.</exception>
    private static string ResolveSourceFile(string filePath, out string tempPath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"The specified file was not found: {filePath}");
        
        var fullFilePath = Path.GetFullPath(filePath);
        tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{Path.GetFileName(fullFilePath)}");
        File.Copy(fullFilePath, tempPath, overwrite: true);
        return fullFilePath;
    }

    /// <summary>
    /// Resolves and validates the full destination file path based on the given save path.
    /// Ensures that the directory structure for the destination file exists, creating it if necessary.
    /// </summary>
    /// <param name="savePath">The intended path to save the destination file, including the file name and extension.</param>
    /// <returns>Returns the absolute path of the destination file.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the output directory cannot be determined from the provided save path.</exception>
    private static string ResolveDesitnationFile(string savePath)
    {
        var fullSavePath = Path.GetFullPath(savePath);
        var saveDirectory = Path.GetDirectoryName(fullSavePath);
        
        if (string.IsNullOrWhiteSpace(saveDirectory))
            throw new InvalidOperationException($"Could not determine output directory from savePath: {savePath}");

        Directory.CreateDirectory(saveDirectory);
        return fullSavePath;
    }
}