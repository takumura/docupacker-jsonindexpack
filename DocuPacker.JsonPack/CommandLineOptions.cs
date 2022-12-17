using CommandLine;

namespace DocuPacker.JsonPack;

public class CommandLineOptions
{
    [Option('i', "input", Required = true, HelpText = "Set target directry(full path) which has markdown files.")]
    public string? Input { get; set; }

    [Option('o', "output", Required = true, HelpText = "Set target directry(full path) to output converted json files.")]
    public string? OutputDir { get; set; }

    [Option('z', "indexDir", Required = false, HelpText = "Optional. If the value (expecting target directry(full path)) is not blank, output index.json which contains whole json data.")]
    public string? IndexDir { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Optional. Set output to verbose messages.")]
    public bool Verbose { get; set; }
}
