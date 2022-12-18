using System.Text;
using Microsoft.Extensions.Logging;

namespace DocuPacker.JsonIndexPack.Utils;

public class FileReader
{
    readonly ILogger logger;
    readonly IPollyRetryPolicy policy;
    readonly int fileStreamBufferSize = 4096;

    public FileReader(ILogger<MarkdownConverterService> _logger, IPollyRetryPolicy _policy)
    {
        logger = _logger;
        policy = _policy;
    }

    public async ValueTask<string> ReadToEndFileAsync(string filePath, CancellationToken cancellationToken)
    {
        logger.LogTrace($"read file: {filePath}");
        var result = string.Empty;

        result = await policy.GetRetryPolicy().ExecuteAsync(async () =>
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, fileStreamBufferSize, FileOptions.Asynchronous);
            using var streamReader = new StreamReader(fileStream, Encoding.UTF8);
            return await streamReader.ReadToEndAsync(cancellationToken);
        });
        return result;
    }
}
