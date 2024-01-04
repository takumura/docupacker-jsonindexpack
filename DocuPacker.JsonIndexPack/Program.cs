using CommandLine;
using DocuPacker.JsonIndexPack.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DocuPacker.JsonIndexPack;

public class Program
{
    readonly ILogger logger;
    readonly ServiceProvider serviceProvider;

    // Referred to the following sample code.
    // https://github.com/aspnet/Logging/blob/master/samples/SampleApp/Program.cs
    public Program(CommandLineOptions? options = null)
    {
        var serviceCollection = new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddZLoggerConsole();

                // bad manner but reviewing args and finding verbose options for rename/preview 
                if (options?.Verbose == true)
                {
                    logging.SetMinimumLevel(LogLevel.Trace);
                }
                else
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                }
            })
            .AddSingleton<IMarkdownConverterService, MarkdownConverterService>()
            .AddSingleton<IPollyRetryPolicy, PollyRetryPolicy>();

        // providers may be added to a LoggerFactory before any loggers are created
        serviceProvider = serviceCollection.BuildServiceProvider();

        // getting the logger using the class's name is conventional
        logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    }


    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = !cts.IsCancellationRequested;
            cts.Cancel();
        };

        await Parser.Default.ParseArguments<CommandLineOptions>(args).MapResult(
                async options => await new Program(options).RunService(options, cts.Token).ConfigureAwait(false),
                async errors => await new Program().ProcessErrors(errors).ConfigureAwait(false));
    }

    async Task RunService(CommandLineOptions options, CancellationToken token)
    {
        try
        {
            logger.LogTrace("Start service");
            var service = serviceProvider.GetRequiredService<IMarkdownConverterService>();
            await service.RunAsync(options, token).ConfigureAwait(false);
            logger.LogInformation("Convert process is successfully completed!");


            if (options.Verbose)
            {
                logger.LogTrace("Press any key to close the window...");
                Console.ReadLine();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "");
        }
    }

    async Task ProcessErrors(IEnumerable<Error> errors)
    {
        await Task.Run(() =>
        {
            foreach (var item in errors)
            {
                logger.LogError(item.ToString());
            }
        });
    }
}

