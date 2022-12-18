using Polly.Retry;

namespace DocuPacker.JsonIndexPack.Utils;

public interface IPollyRetryPolicy
{
    AsyncRetryPolicy GetRetryPolicy();
}
