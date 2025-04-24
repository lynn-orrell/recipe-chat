using System.Diagnostics;
using System.Text.Json;
using RecipeChat.PromptTemplates.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using RecipeChat.Plugins;
using Microsoft.Extensions.Logging;
using RecipeChat.GroupChat.Models;
using System.Reflection;


namespace RecipeChat.GroupChat
{
    public class RecipeGroupChat
    {
        public event EventHandler<GroupChatResponseGeneratedEventArgs>? GroupChatResponseGenerated;
        public event EventHandler<AgentSelectionStrategyResponse>? GroupChatSelectionStrategyResult;
        public event EventHandler<AgentTerminationStrategyResponse>? GroupChatTerminationStrategyResult;
        private readonly ILogger<RecipeGroupChat> _logger;
        private readonly Kernel _kernel;
        private readonly ActivitySource _activitySource;
        private AgentGroupChat? _chat;

        public RecipeGroupChat(ILogger<RecipeGroupChat> logger, Kernel kernel, ActivitySource activitySource)
        {
            _logger = logger;
            _kernel = kernel;
            _activitySource = activitySource;
        }

        public async Task AddChatMessageAsync(string message)
        {
            await Initialize();
            _chat!.AddChatMessage(new ChatMessageContent(AuthorRole.User, message));            
        }

        public async Task AddChatMessagesAsync(IReadOnlyList<ChatMessageContent> messages)
        {
            await Initialize();
            _chat!.AddChatMessages(messages);
        }

        public async Task StartGroupChat()
        {
            await Initialize();

            do
            {
                await foreach (ChatMessageContent response in _chat!.InvokeAsync())
                {
                    OnGroupChatResponseGenerated(new GroupChatResponseGeneratedEventArgs(response));
                }
            } while (!_chat.IsComplete);
        }

        protected virtual void OnGroupChatResponseGenerated(GroupChatResponseGeneratedEventArgs e)
        {
            if (GroupChatResponseGenerated != null)
            {
                GroupChatResponseGenerated?.Invoke(this, e);
            }
        }

        protected virtual void OnGroupChatSelectionStrategyResult(AgentSelectionStrategyResponse e)
        {
            if (GroupChatSelectionStrategyResult != null)
            {
                GroupChatSelectionStrategyResult?.Invoke(this, e);
            }
        }

        protected virtual void OnGroupChatTerminationStrategyResult(AgentTerminationStrategyResponse e)
        {
            if (GroupChatTerminationStrategyResult != null)
            {
                GroupChatTerminationStrategyResult?.Invoke(this, e);
            }
        }

        private async Task Initialize()
        {
            if (_chat == null)
            {
                _kernel.ImportPluginFromType<IngredientsPlugin>("IngredientsPlugin");

                using var activity = _activitySource.StartActivity("ExecuteAsync");

                ChatCompletionAgent recipeBuilderAgent = await CreateChatCompletionAgentAsync(GetPath("PromptTemplates/Agents/RecipeBuilderAgent.yaml"), _kernel);
                ChatCompletionAgent glutenFreeReviewerAgent = await CreateChatCompletionAgentAsync(GetPath("PromptTemplates/Agents/GlutenFreeReviewerAgent.yaml"), _kernel);
                ChatCompletionAgent veganReviewerAgent = await CreateChatCompletionAgentAsync(GetPath("PromptTemplates/Agents/VeganReviewerAgent.yaml"), _kernel);

                KernelFunctionSelectionStrategy selectionStrategy = await CreateSelectionStrategy(GetPath("PromptTemplates/Strategies/AgentSelectionStrategy.yaml"), _kernel, recipeBuilderAgent);
                KernelFunctionTerminationStrategy terminationStrategy = await CreateTerminationStrategy(GetPath("PromptTemplates/Strategies/AgentTerminationStrategy.yaml"), _kernel);

                _chat = new(recipeBuilderAgent, glutenFreeReviewerAgent, veganReviewerAgent)
                {
                    ExecutionSettings = new()
                    {
                        SelectionStrategy = selectionStrategy,
                        TerminationStrategy = terminationStrategy
                    }
                };
            }
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
                    OnGroupChatSelectionStrategyResult(selectionResponse!);
                    return selectionResponse!.NextAgent;
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
                    OnGroupChatTerminationStrategyResult(terminationResponse!);
                    return terminationResponse!.ShouldTerminate;
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

        private string GetPath(string file)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, file);
        }
    }
}