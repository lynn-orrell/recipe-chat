using RecipeChat.PromptTemplates.Models;

namespace RecipeChat.GroupChat.Models
{
    public class GroupChatSelectionStrategyResponseEventArgs : EventArgs
    {
        public AgentSelectionStrategyResponse AgentSelectionStrategyResponse { get; }

        public GroupChatSelectionStrategyResponseEventArgs(AgentSelectionStrategyResponse agentSelectionStrategyResponse)
        {
            AgentSelectionStrategyResponse = agentSelectionStrategyResponse;
        }
    }
}