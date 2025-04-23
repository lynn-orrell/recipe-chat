using System.Diagnostics;
using System.Text.Json;
using RecipeChat.PromptTemplates.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using RecipeChat.Plugins;

namespace RecipeChat;

public class Worker : BackgroundService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<Worker> _logger;
    private readonly Kernel _kernel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ActivitySource _activitySource;

    public Worker(IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger, Kernel kernel, IHttpClientFactory httpClientFactory, ActivitySource activitySource)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _kernel = kernel;
        _httpClientFactory = httpClientFactory;
        _activitySource = activitySource;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _kernel.ImportPluginFromType<IngredientsPlugin>("IngredientsPlugin");
        
        using var activity = _activitySource.StartActivity("ExecuteAsync");

        ChatCompletionAgent recipeBuilderAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/RecipeBuilderAgent.yaml", _kernel);
        ChatCompletionAgent glutenFreeReviewerAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/GlutenFreeReviewerAgent.yaml", _kernel);
        ChatCompletionAgent veganReviewerAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/VeganReviewerAgent.yaml", _kernel);

        KernelFunctionSelectionStrategy selectionStrategy = await CreateSelectionStrategy("PromptTemplates/Strategies/AgentSelectionStrategy.yaml", _kernel, recipeBuilderAgent);
        KernelFunctionTerminationStrategy terminationStrategy = await CreateTerminationStrategy("PromptTemplates/Strategies/AgentTerminationStrategy.yaml", _kernel);

        AgentGroupChat chat = new(recipeBuilderAgent, glutenFreeReviewerAgent, veganReviewerAgent)
        {
            ExecutionSettings = new()
            {
                SelectionStrategy = selectionStrategy,
                TerminationStrategy = terminationStrategy
            }
        };

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("ASSISTANT: How can I help you? Type 'exit' to quit.");
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("USER: ");
            string? userInput = Console.ReadLine();
            Console.ResetColor();
            if (userInput == null || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await AddChatMessageAsync(chat, userInput);
        }

        _hostApplicationLifetime.StopApplication();
    }

    private async Task AddChatMessageAsync(AgentGroupChat chat, string message)
    {
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, message));
        do
        {
            await foreach (ChatMessageContent response in chat.InvokeAsync())
            {
                PrettyPrint(response, response.Content ?? "<No Content>");
            }
        } while (!chat.IsComplete);
    }

    private void PrettyPrint(ChatMessageContent chatMessageContent, string message)
    {
        Console.ForegroundColor = chatMessageContent.Role.Equals(AuthorRole.User) ? ConsoleColor.Yellow :
                                  chatMessageContent.Role.Equals(AuthorRole.Assistant) ? ConsoleColor.Gray : 
                                  ConsoleColor.White;

        Console.WriteLine($"{chatMessageContent.Role.Label.ToUpper()} [{chatMessageContent.AuthorName}]: {message}");
        Console.WriteLine();
        Console.ResetColor();
    }

    private async Task<ChatCompletionAgent> CreateChatCompletionAgentAsync(string promptTemplatePath, Kernel kernel, KernelArguments? kernelArgs = null)
    {
        var agentPrompt = await File.ReadAllTextAsync(promptTemplatePath);
        var agentPromptTemplate = KernelFunctionYaml.ToPromptTemplateConfig(agentPrompt);
        var agent = new ChatCompletionAgent(agentPromptTemplate, new KernelPromptTemplateFactory())
        {
            Kernel = kernel,
            Arguments = new KernelArguments(kernelArgs ?? new KernelArguments(), agentPromptTemplate.ExecutionSettings)
        };

        return agent;
    }

    private async Task<KernelFunctionSelectionStrategy> CreateSelectionStrategy(string promptTemplatePath, Kernel kernel, Agent? initialAgent = null)
    {
        KernelFunction agentSelectionFunction = KernelFunctionYaml.FromPromptYaml(await File.ReadAllTextAsync(promptTemplatePath));
        
        KernelFunctionSelectionStrategy selectionStrategy = new(agentSelectionFunction, kernel)
        {
            ResultParser = (result) =>
            {
                var selectionResponse = JsonSerializer.Deserialize<AgentSelectionStrategyResponse>(result.GetValue<string>()!);
                return selectionResponse.NextAgent;
            },
            AgentsVariableName = "agents",
            HistoryVariableName = "history",
            InitialAgent = initialAgent,
            UseInitialAgentAsFallback = true
        };

        return selectionStrategy;
    }

    private async Task<KernelFunctionTerminationStrategy> CreateTerminationStrategy(string promptTemplatePath, Kernel kernel, Agent[]? agentsAllowedToTerminate = null)
    {
        KernelFunction agentTerminationFunction = KernelFunctionYaml.FromPromptYaml(await File.ReadAllTextAsync(promptTemplatePath));
        
        KernelFunctionTerminationStrategy terminationStrategy = new(agentTerminationFunction, kernel)
        {
            ResultParser = (result) => 
            {
                var terminationResponse = JsonSerializer.Deserialize<AgentTerminationStrategyResponse>(result.GetValue<string>()!);
                return terminationResponse.ShouldTerminate;
            },
            AgentVariableName = "agents",
            HistoryVariableName = "history",
            HistoryReducer = new ChatHistoryTruncationReducer(3),
            MaximumIterations = 2,
            Agents = agentsAllowedToTerminate,
            AutomaticReset = true
        };

        return terminationStrategy;
    }
}