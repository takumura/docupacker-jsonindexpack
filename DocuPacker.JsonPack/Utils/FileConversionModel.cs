namespace DocuPacker.JsonIndexPack.Utils;

public class FileConversionModel
{
    public string FileName { get; set; } = string.Empty;
    public string OutputDir { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string MdFilePath { get; set; } = string.Empty;
    public FileConversionModelStatusEnum Status { get; set; } = FileConversionModelStatusEnum.Confirming;
    public string JsonFilePath => $"{OutputDir}{Path.DirectorySeparatorChar}{RelativePath}{Path.DirectorySeparatorChar}{FileName}.json";
}
