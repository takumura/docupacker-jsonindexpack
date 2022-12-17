using YamlDotNet.Serialization;
using JsonObject = DynaJson.JsonObject;

namespace DocuPacker.JsonPack.Utils;

public class MarkdownConverter
{
    readonly IDeserializer yamlDeserializer;
    readonly ISerializer yamlToJsonSerializer;

    public MarkdownConverter()
    {
        yamlDeserializer = new DeserializerBuilder().Build();
        yamlToJsonSerializer = new SerializerBuilder().JsonCompatible().Build();
    }

    public string ConvertMarkDownTextToJson(string markdownText)
    {
        string result;

        var contents = SplitMarkdownContents(markdownText);

        // read yaml frontmatter and get serialized json string
        using (var reader = new StringReader(contents.Item1))
        {
            var yamlObject = yamlDeserializer.Deserialize(reader);

            // if there is no frontmatter, return empty, and skip creating json file
            if (yamlObject == null) return "";

            result = yamlToJsonSerializer.Serialize(yamlObject);
        }

        // parse json string by DynaJson and add body content
        var jsonObject = JsonObject.Parse(result);
        jsonObject.body = contents.Item2;
        return jsonObject.ToString();
    }

    public string GetIndexJsonString((string, string)[] jsonFiles)
    {
        var indexJson = new string[jsonFiles.Count()];

        // jsonFiles array is generated using parallel.ForEachAsync, then the data will be pushed randomly.
        // It will cause generating the different hash value however the data is actually the same.
        // Sorting array by first element = docRef to avoid that situation.
        jsonFiles = jsonFiles.OrderBy(x => x.Item1).ToArray();

        foreach (var (jsonFile, index) in jsonFiles.Select((item, index) => (item, index)))
        {
            var elem = JsonObject.Parse("{}");
            // align DirectorySeparatorChar to slash
            elem.docRef = jsonFile.Item1.Replace("\\", "/");
            elem.content = jsonFile.Item2;
            indexJson[index] = elem.ToString();
        }
        var resultJson = JsonObject.Serialize(indexJson);
        return resultJson;
    }

    private (string, string) SplitMarkdownContents(string markdownText)
    {
        var contents = markdownText.Split("---");

        // target data is expected to be started from "---", means frontmater splitter.
        // so the contents[0] will be blank, contenst[1] will be frontmatter and remaining array will be body.
        var frontmatter = contents[1];

        string? body;
        if (contents.Length == 3)
        {
            // frontmatter splitter is not included on body
            body = contents[2];
        }
        else
        {
            // at least one frontmatter splitter is included on body, concatenating all array after index=3 as body.
            var requiredContents = contents.Skip(2).ToArray();
            body = string.Join("---", requiredContents).Trim();
        }

        var result = (frontmatter, body);
        return result;
    }
}