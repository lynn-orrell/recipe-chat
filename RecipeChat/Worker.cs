using System.Diagnostics;
using System.Text.Json;
using FlightChat.PromptTemplates.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry.Trace;

namespace FlightChat;

public class Worker : BackgroundService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Kernel _kernel;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ActivitySource _activitySource;

    public Worker(IHostApplicationLifetime hostApplicationLifetime, Kernel kernel, IHttpClientFactory httpClientFactory, ActivitySource activitySource)
    {
        _hostApplicationLifetime = hostApplicationLifetime;
        _kernel = kernel;
        _httpClientFactory = httpClientFactory;
        _activitySource = activitySource;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("ExecuteAsync");

        ChatCompletionAgent recipeBuilderAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/RecipeBuilderAgent.yaml", _kernel);
        ChatCompletionAgent glutenFreeReviewerAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/GlutenFreeReviewerAgent.yaml", _kernel);
        ChatCompletionAgent veganReviewerAgent = await CreateChatCompletionAgentAsync("PromptTemplates/Agents/VeganReviewerAgent.yaml", _kernel);

        KernelFunctionSelectionStrategy selectionStrategy = await CreateSelectionStrategy("PromptTemplates/Strategies/AgentSelectionStrategy.yaml", _kernel);
        KernelFunctionTerminationStrategy terminationStrategy = await CreateTerminationStrategy("PromptTemplates/Strategies/AgentTerminationStrategy.yaml", _kernel, new[] 
        { 
            recipeBuilderAgent,
            glutenFreeReviewerAgent,
            veganReviewerAgent
        });

        AgentGroupChat chat = new(recipeBuilderAgent, glutenFreeReviewerAgent, veganReviewerAgent)
        {
            ExecutionSettings = new()
            {
                SelectionStrategy = selectionStrategy,
                TerminationStrategy = terminationStrategy
            }
        };

        await AddChatMessageAsync(chat, "Please create a main dish from the following ingredients: Chicken Breast, Tomatoes, Onions, Garlic, Olive Oil, Heavy Cream, Pasta");
        await AddChatMessageAsync(chat, "I'm allergic to gluten");
        await AddChatMessageAsync(chat, "Can I also get a vegan friendly version?");

        _hostApplicationLifetime.StopApplication();
    }

    private async Task AddChatMessageAsync(AgentGroupChat chat, string message)
    {
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, message));
        do
        {
            await foreach (ChatMessageContent response in chat.InvokeAsync())
            {
                // _logger.LogInformation($"{response.ToString()}");
            }
        } while (!chat.IsComplete);
    }

    private async Task<ChatCompletionAgent> CreateChatCompletionAgentAsync(string promptTemplatePath, Kernel kernel, KernelArguments? kernelArgs = null)
    {
        var agentPrompt = await File.ReadAllTextAsync(promptTemplatePath);
        var agentPromptTemplate = KernelFunctionYaml.ToPromptTemplateConfig(agentPrompt);
        var agent = new ChatCompletionAgent(agentPromptTemplate)
        {
            Kernel = kernel,
            Arguments = new KernelArguments(kernelArgs ?? new KernelArguments(), agentPromptTemplate.ExecutionSettings)
        };

        return agent;
    }

    private async Task<KernelFunctionSelectionStrategy> CreateSelectionStrategy(string promptTemplatePath, Kernel kernel)
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
            HistoryVariableName = "history"
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
            HistoryReducer = new ChatHistoryTruncationReducer(1),
            MaximumIterations = 2,
            Agents = agentsAllowedToTerminate,
            AutomaticReset = true
        };

        return terminationStrategy;
    }
}