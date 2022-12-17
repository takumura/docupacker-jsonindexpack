using Polly.Retry;

namespace DocuPacker.JsonPack.Utils;

public interface IPollyRetryPolicy
{
    AsyncRetryPolicy GetRetryPolicy();
}
