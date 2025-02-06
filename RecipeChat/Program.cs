using System.Diagnostics;
using Azure.Identity;
using dotenv.net;
using FlightChat;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

DotEnv.Fluent().WithProbeForEnv().Load();

string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_ENDPOINT' is not set.");

string deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("Environment variable 'AZURE_OPENAI_DEPLOYMENT_NAME' is not set.");

Uri otelEndpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_ENDPOINT")
    ?? throw new InvalidOperationException("Environment variable 'OTEL_ENDPOINT' is not set."));

bool logHttpRequests = bool.Parse(Environment.GetEnvironmentVariable("LOG_HTTP_REQUESTS") ?? "false");

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);

var builder = Host.CreateApplicationBuilder(args);

ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault().AddService("RecipeChat");

var traceProviderBuilder = Sdk.CreateTracerProviderBuilder()
                              .SetResourceBuilder(resourceBuilder)
                              .AddSource("RecipeChat")
                              .AddSource("Microsoft.SemanticKernel*")
                              .AddOtlpExporter(options => options.Endpoint = otelEndpoint)
                              .AddConsoleExporter();

if (logHttpRequests)
{
    traceProviderBuilder.AddHttpClientInstrumentation(options =>
    {
        options.EnrichWithHttpRequestMessage = async (activity, httpRequestMessage) =>
        {
            if(httpRequestMessage.Content == null) 
                return;

            await httpRequestMessage.Content.LoadIntoBufferAsync();
            MemoryStream ms = new();
            await httpRequestMessage.Content.CopyToAsync(ms);
            ms.Seek(0L, SeekOrigin.Begin);
            using StreamReader reader = new(ms);
            string content = reader.ReadToEnd();
            activity.SetTag("http.request.body", content);
        };
        options.EnrichWithHttpResponseMessage = async (activity, httpResponseMessage) =>
        {
            if(httpResponseMessage.Content == null) 
                return;

            await httpResponseMessage.Content.LoadIntoBufferAsync();
            MemoryStream ms = new();
            await httpResponseMessage.Content.CopyToAsync(ms);
            ms.Seek(0L, SeekOrigin.Begin);
            using StreamReader reader = new(ms);
            string content = reader.ReadToEnd();
            activity.SetTag("http.response.body", content);
        };
    });
}

using var traceProvider = traceProviderBuilder.Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
                             .SetResourceBuilder(resourceBuilder)
                             .AddMeter("Microsoft.SemanticKernel*")
                             .AddOtlpExporter(options => options.Endpoint = otelEndpoint)
                             .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.AddOtlpExporter(options => options.Endpoint = otelEndpoint);
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

ActivitySource recipeChatActivitySource = new("RecipeChat");

builder.Services.AddSingleton(traceProvider);
builder.Services.AddSingleton(meterProvider);
builder.Services.AddSingleton(loggerFactory);
builder.Services.AddSingleton(recipeChatActivitySource);
builder.Services.AddHttpClient();
builder.Services.AddHostedService<Worker>();

builder.Services.AddSingleton<IKernelBuilder>(serviceProvider =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.Services.AddSingleton(traceProvider);
    kernelBuilder.Services.AddSingleton(meterProvider);
    kernelBuilder.Services.AddSingleton(loggerFactory);
    kernelBuilder.Services.AddSingleton(recipeChatActivitySource);
    kernelBuilder.Services.AddHttpClient();
    kernelBuilder.Services.AddAzureOpenAIChatCompletion(deployment, endpoint, new ChainedTokenCredential(new AzureCliCredential(), new ManagedIdentityCredential()));
    // kernelBuilder.Services.AddOllamaChatCompletion("llama3-groq-tool-use:8b", new Uri("http://localhost:11434"));
    // kernelBuilder.Plugins.AddFromType<RecipeChatPlugin>();

    return kernelBuilder;
});

var host = builder.Build();
host.Run();
