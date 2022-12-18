using DocuPacker.JsonIndexPack.Utils;
using Microsoft.Extensions.Logging;

namespace DocuPacker.JsonIndexPack;

public class MarkdownConverterService : IMarkdownConverterService
{
    readonly FileManager fileManager;
    readonly ILogger logger;

    public MarkdownConverterService(ILogger<MarkdownConverterService> _logger, IPollyRetryPolicy _policy)
    {
        fileManager = new FileManager(_logger, _policy);
        logger = _logger;
    }

    public async ValueTask ConvertAsync(string? source, string? outputDir, string? indexDir, DateTime? dateFrom, CancellationToken token)
    {
        try
        {
            // Validate source(expecting single file or directory) and destination(json directory)
            if (string.IsNullOrWhiteSpace(source))
            {
                logger.LogError("input option should be set");
                return;
            }
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                logger.LogError("output option should be set");
                return;
            }

            // get updated item (single file or list) from source
            var updates = fileManager.GetUpdatesFromSource(source);


            // get current item list from destinationDirectory
            logger.LogInformation($"target destination directory: {outputDir}");
            var currentItems = fileManager.GetCurrentItemsFromDestination(outputDir);

            // both current and update have no item, we have no task to do
            if (!updates.Any() && !currentItems.Any())
            {
                logger.LogError($"no directory or file to convert: {source}");
                return;
            }

            // prepare for the input and output directory path
            var inputPath = source;
            if (File.Exists(source)) inputPath = Path.GetDirectoryName(source) ?? "";

            var outputPath = outputDir;

            // get file conversion model list by comparing current and updated items
            var currentFileComparisonModels = fileManager.GenerateFileComparisonModels(currentItems, outputPath);
            var updatedFileComparisonModels = fileManager.GenerateFileComparisonModels(updates, inputPath);
            var fileConversionModels = fileManager.GenerateFileConversionModels(currentFileComparisonModels, updatedFileComparisonModels, outputPath, dateFrom);

            // remove json file if it's not required anymore
            fileManager.RemoveDeletedFilesFromOutputDir(fileConversionModels.Where(x => x.Status == FileConversionModelStatusEnum.Deleted).ToList());

            // if the hash is changed, update json file
            await fileManager.UpdateJsonFileIfRequired(fileConversionModels.Where(x => x.Status == FileConversionModelStatusEnum.Confirming).ToList(), token);

            // create new json file
            await fileManager.CreateAddedFilesAsync(fileConversionModels.Where(x => x.Status == FileConversionModelStatusEnum.Added).ToList(), token);

            if (!string.IsNullOrEmpty(indexDir))
            {
                await fileManager.CreateIndexJsonFileIfRequiredAsync(outputDir, indexDir, token).ConfigureAwait(false);
            }

        }
        catch (Exception)
        {
            throw;
        }
    }

    public async ValueTask RunAsync(CommandLineOptions options, CancellationToken token)
    {
        try
        {
            await ConvertAsync(options.Input, options.OutputDir, options.IndexDir, null, token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw;
        }
    }
}
