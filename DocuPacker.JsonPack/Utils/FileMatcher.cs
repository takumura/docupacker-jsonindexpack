using Microsoft.Extensions.FileSystemGlobbing;

namespace DocuPacker.JsonPack.Utils;

public class FileMatcher
{
    readonly Matcher matcher = new();

    public List<string> GetResultsInFullPath(string dirPath, string[] includePatterns, string[] excludePattersn)
    {
        // use new file globbing library
        // see https://docs.microsoft.com/ja-jp/dotnet/core/extensions/file-globbing
        matcher.AddIncludePatterns(includePatterns);
        matcher.AddExcludePatterns(excludePattersn);
        var matchingFiles = matcher.GetResultsInFullPath(dirPath);
        return matchingFiles.ToList();
    }
}
