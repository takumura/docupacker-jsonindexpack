using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DocuPacker.JsonIndexPack.Utils;

public class FileManager
{
    readonly MarkdownConverter converter;
    readonly FileMatcher matcher;
    readonly FileReader reader;
    readonly FileWriter writer;
    readonly ILogger logger;
    readonly int maxDegreeOfParallelism;

    public FileManager(ILogger<MarkdownConverterService> _logger, IPollyRetryPolicy _policy)
    {
        converter = new();
        matcher = new();
        reader = new(_logger, _policy);
        writer = new(_logger, _policy);
        logger = _logger;

        // use 75% of logical processors for parallel.ForEach and parallel.ForEachAsync
        // https://learn.microsoft.com/ja-jp/dotnet/api/system.environment.processorcount?view=net-7.0
        maxDegreeOfParallelism = Convert.ToInt32(Math.Ceiling(Environment.ProcessorCount * 0.75));
        logger.LogTrace($"set MaxDegreeOfParallelism to {maxDegreeOfParallelism}");
    }

    public async ValueTask CreateAddedFilesAsync(List<FileConversionModel> addFiles, CancellationToken token)
    {
        try
        {
            var fileCount = addFiles.Count();
            if (fileCount == 0)
            {
                logger.LogInformation("No target files found to add");
                return;
            }

            logger.LogInformation($"{fileCount} target files found to add");
            var parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = token
            };

            await Parallel.ForEachAsync(addFiles, parallelOptions, async (file, pToken) =>
            {
                pToken.ThrowIfCancellationRequested();

                var fileText = await reader.ReadToEndFileAsync(file.MdFilePath, pToken).ConfigureAwait(false);

                var jsonText = converter.ConvertMarkDownTextToJson(fileText);
                if (string.IsNullOrEmpty(jsonText)) return;

                await writer.WriteJsonFileAsync(jsonText, file.JsonFilePath, pToken).ConfigureAwait(false);
            });
        }
        catch (OperationCanceledException e)
        {
            logger.LogInformation($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async ValueTask CreateIndexJsonFileIfRequiredAsync(string destinationDir, string indexDir, CancellationToken token)
    {
        logger.LogInformation($"generate index.json to index directory: {indexDir}");
        var indexFilePath = $"{indexDir}{Path.DirectorySeparatorChar}index.json";

        var jsonFiles = GetCurrentItemsFromDestination(destinationDir);
        if (!jsonFiles.Any())
        {
            logger.LogError("there is no converted json file: {destination}", destinationDir);
            return;
        }

        if (!Directory.Exists(indexDir))
        {
            logger.LogTrace($"create new directory: {indexDir}");
            Directory.CreateDirectory(indexDir);
        }

        // read all json file and put data to concurrentBag
        ConcurrentBag<(string, string)> concurrentList = new();
        var parallelOptions = new ParallelOptions()
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism,
            CancellationToken = token
        };

        await Parallel.ForEachAsync(jsonFiles, parallelOptions, async (file, pToken) =>
        {
            pToken.ThrowIfCancellationRequested();

            var jsonData = await reader.ReadToEndFileAsync(file, pToken).ConfigureAwait(false);
            var relativePath = Path.GetDirectoryName(Path.GetRelativePath(destinationDir, file));
            var fileName = Path.GetFileNameWithoutExtension(file);
            var docRef = string.IsNullOrEmpty(relativePath)
                ? fileName
                : $"{relativePath}{Path.DirectorySeparatorChar}{fileName}";
            concurrentList.Add((docRef, jsonData));
        });

        var jsonString = converter.GetIndexJsonString(concurrentList.ToArray());

        if (File.Exists(indexFilePath))
        {
            logger.LogTrace("index.json is alrady generated, check hash to detect diff");

            // get new json hash
            var newHash = GetSha1Hash(jsonString);

            // get original json hash
            var originalJsonData = await reader.ReadToEndFileAsync(indexFilePath, token).ConfigureAwait(false);
            var originalHash = GetSha1Hash(originalJsonData);

            // if hash is different, write new json file to destination folder
            if (newHash.SequenceEqual(originalHash))
            {
                logger.LogInformation("data is not changed, skip generating index.json");
                return;
            }
        }

        await writer.WriteJsonFileAsync(jsonString, indexFilePath, token).ConfigureAwait(false);
    }

    public List<FileComparisonModel> GenerateFileComparisonModels(List<string> files, string fromDir)
    {
        List<FileComparisonModel> result = new();
        foreach (var fullPath in files)
        {
            result.Add(new FileComparisonModel()
            {
                FullPath = fullPath,
                DirectoryFrom = fromDir,
                RelativePath = Path.GetDirectoryName(Path.GetRelativePath(fromDir, fullPath)) ?? "",
                FileName = Path.GetFileNameWithoutExtension(fullPath)
            });
        }
        return result;
    }

    public List<FileConversionModel> GenerateFileConversionModels(
       List<FileComparisonModel> currentFileComparisonModels,
       List<FileComparisonModel> updatedFileComparisonModels,
       string outputDir,
       DateTime? dateFrom)
    {
        List<FileConversionModel> result = new();

        // check from current items
        // if filename is NOT found with same relative path, it will be "Deleted"
        // if same filename found with same relative path, it will be "NotChanged" or "Updated"
        foreach (var jsonFile in currentFileComparisonModels)
        {
            if (updatedFileComparisonModels.Any(mdFile => mdFile.RelativePath == jsonFile.RelativePath && mdFile.FileName == jsonFile.FileName))
            {
                // do nothing as this scenario has been already covered by previous "current -> updated" check
            }
            else
            {
                result.Add(new FileConversionModel()
                {
                    FileName = jsonFile.FileName,
                    OutputDir = outputDir,
                    RelativePath = jsonFile.RelativePath,
                    Status = FileConversionModelStatusEnum.Deleted,
                });
            }
        }

        // check from updates.
        // if filename is NOT found with same relative path, it will be "Added"
        // if same filename found with same relative path, it will be "NotChanged" or "Updated"
        foreach (var mdFile in updatedFileComparisonModels)
        {
            if (currentFileComparisonModels.Any(x => x.RelativePath == mdFile.RelativePath && x.FileName == mdFile.FileName))
            {
                if (dateFrom.HasValue && File.GetLastWriteTime(mdFile.FullPath) < dateFrom) continue;

                result.Add(new FileConversionModel()
                {
                    FileName = mdFile.FileName,
                    OutputDir = outputDir,
                    RelativePath = mdFile.RelativePath,
                    Status = FileConversionModelStatusEnum.Confirming, // to be confirmed on later step, by comparing hash value
                    MdFilePath = mdFile.FullPath,
                });
            }
            else
            {
                result.Add(new FileConversionModel()
                {
                    FileName = mdFile.FileName,
                    OutputDir = outputDir,
                    RelativePath = mdFile.RelativePath,
                    Status = FileConversionModelStatusEnum.Added,
                    MdFilePath = mdFile.FullPath,
                });
            }
        }

        return result;
    }

    public List<string> GetCurrentItemsFromDestination(string destinationDir)
    {
        // destination folder will possibly not exist for initial conversion
        if (!Directory.Exists(destinationDir))
        {
            logger.LogTrace($"create new directory: {destinationDir}");
            Directory.CreateDirectory(destinationDir);
        }

        var includePatterns = new[] { "**/*.json" };
        var excludePatterns = new[] { "tmp/*", "temp/*", "index.json" };
        return matcher.GetResultsInFullPath(destinationDir, includePatterns, excludePatterns);
    }

    public List<string> GetUpdatesFromSource(string source)
    {
        List<string> result = new();
        if (File.Exists(source))
        {
            logger.LogInformation($"target source file: {source}");
            result.Add(source);
        }
        else if (Directory.Exists(source))
        {
            logger.LogInformation($"target source directory: {source}");
            var includePatterns = new[] { "**/*.md" };
            var excludePatterns = new[] { "tmp/*", "temp/*", "**/_*.md" };
            return matcher.GetResultsInFullPath(source, includePatterns, excludePatterns);
        }

        return result;
    }

    public void RemoveDeletedFilesFromOutputDir(List<FileConversionModel> removeFiles)
    {
        try
        {
            var fileCount = removeFiles.Count();
            if (fileCount == 0)
            {
                logger.LogInformation("No target files found to delete");
                return;
            }

            logger.LogInformation($"{fileCount} target files found to delete");
            var files = removeFiles.Select(x => new FileInfo(x.JsonFilePath));
            var parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            Parallel.ForEach(files, parallelOptions, file =>
            {
                var parentDir = file.Directory;

                logger.LogTrace($"delete file: {file.FullName}");
                file.Delete();

                // if there is no other files and subdirectories, delete parent folder.
                if (parentDir != null && parentDir.GetDirectories().Length == 0 && parentDir.GetFiles().Length == 0)
                {
                    parentDir.Delete();
                }

            });
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async ValueTask UpdateJsonFileIfRequired(IEnumerable<FileConversionModel> targetFiles, CancellationToken token)
    {
        try
        {
            var fileCount = targetFiles.Count();
            if (fileCount == 0)
            {
                logger.LogInformation("No target files found to check updates");
                return;
            }

            logger.LogInformation($"{fileCount} target files found to check updates");
            var parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = token
            };
            await Parallel.ForEachAsync(targetFiles, parallelOptions, async (file, pToken) =>
            {
                token.ThrowIfCancellationRequested();

                var fileText = await reader.ReadToEndFileAsync(file.MdFilePath, pToken).ConfigureAwait(false);

                // convert markdown file to json
                var jsonText = converter.ConvertMarkDownTextToJson(fileText);
                if (string.IsNullOrEmpty(jsonText)) return;

                // get new json hash
                var newHash = GetSha1Hash(jsonText);

                // get original json hash
                var originalJsonData = await reader.ReadToEndFileAsync(file.JsonFilePath, pToken).ConfigureAwait(false);
                var originalHash = GetSha1Hash(originalJsonData);

                // if hash is different, write new json file to destination folder
                if (!newHash.SequenceEqual(originalHash))
                {
                    await writer.WriteJsonFileAsync(jsonText, file.JsonFilePath, pToken).ConfigureAwait(false);
                }
            });
        }
        catch (OperationCanceledException e)
        {
            logger.LogInformation($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
        }
        catch (Exception)
        {
            throw;
        }
    }

    private byte[] GetSha1Hash(string source)
    {
        var byteSource = Encoding.UTF8.GetBytes(source);
        HashAlgorithm sha = SHA1.Create();
        return sha.ComputeHash(byteSource);
    }
}
