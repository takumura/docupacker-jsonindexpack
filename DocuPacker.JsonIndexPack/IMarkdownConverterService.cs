namespace DocuPacker.JsonIndexPack;

public interface IMarkdownConverterService
{
    ValueTask ConvertAsync(string? input, string? output, string? indexDir, DateTime? dateFrom, CancellationToken token);
    ValueTask RunAsync(CommandLineOptions options, CancellationToken token);
}
