using System.Diagnostics;
using Azure.Identity;
using dotenv.net;
using RecipeChat;
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

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(traceProvider);
builder.Services.AddSingleton(meterProvider);
builder.Services.AddSingleton(loggerFactory);
builder.Services.AddSingleton(recipeChatActivitySource);
builder.Services.AddHttpClient();

builder.Services.AddAzureOpenAIChatCompletion(deployment, endpoint, new ChainedTokenCredential(new AzureCliCredential(), new ManagedIdentityCredential()));
// builder.Services.AddOllamaChatCompletion("llama3-groq-tool-use:8b", new Uri("http://localhost:11434"));
builder.Services.AddKernel();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
