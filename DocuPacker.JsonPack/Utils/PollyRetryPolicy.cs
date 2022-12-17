using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DocuPacker.JsonPack.Utils;

public class PollyRetryPolicy: IPollyRetryPolicy
{
    readonly ILogger logger;
    public PollyRetryPolicy(ILogger<MarkdownConverterService> _logger)
    {
        logger = _logger;
    }

    public AsyncRetryPolicy GetRetryPolicy()
    {
        return Policy.Handle<IOException>()
            .WaitAndRetryAsync(4,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, retryCount) =>
                {
                    logger.LogInformation($"catch IOException, retrying... ({retryCount})");
                }
            );
    }
}

