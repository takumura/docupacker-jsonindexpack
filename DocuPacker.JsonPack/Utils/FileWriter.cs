using Microsoft.Extensions.Logging;
using Polly.Retry;

namespace DocuPacker.JsonPack.Utils;

public class FileWriter
{
    readonly ILogger logger;
    readonly IPollyRetryPolicy policy;
    readonly int fileStreamBufferSize = 4096;

    public FileWriter(ILogger<MarkdownConverterService> _logger, IPollyRetryPolicy _policy)
    {
        logger = _logger;
        policy = _policy;
    }

    public async ValueTask WriteJsonFileAsync(string jsonText, string jsonFilePath, CancellationToken cancellationToken)
    {
        logger.LogTrace($"write file: {jsonFilePath}");
        var targetFolder = Path.GetDirectoryName(jsonFilePath) ?? "";
        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        await policy.GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var fileStream = new FileStream(jsonFilePath, FileMode.Create, FileAccess.Write, FileShare.Write, fileStreamBufferSize, FileOptions.Asynchronous);
            using var streamWriter = new StreamWriter(fileStream);
            await streamWriter.WriteAsync(jsonText.AsMemory(), cancellationToken);
        });
    }
}
