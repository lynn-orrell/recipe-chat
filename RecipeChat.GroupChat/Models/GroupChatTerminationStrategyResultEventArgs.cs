using RecipeChat.PromptTemplates.Models;

namespace RecipeChat.GroupChat.Models
{
    public class GroupChatTerminationStrategyResponseEventArgs : EventArgs
    {
        public AgentTerminationStrategyResponse AgentTerminationStrategyResponse { get; }

        public GroupChatTerminationStrategyResponseEventArgs(AgentTerminationStrategyResponse agentTerminationStrategyResponse)
        {
            AgentTerminationStrategyResponse = agentTerminationStrategyResponse;
        }
    }
}