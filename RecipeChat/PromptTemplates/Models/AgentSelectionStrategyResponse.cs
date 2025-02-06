using System.Text.Json.Serialization;

namespace FlightChat.PromptTemplates.Models
{
    public class AgentSelectionStrategyResponse
    {
        [JsonPropertyName("next_agent")]
        public required string NextAgent { get; init; }

        [JsonPropertyName("selection_reason")]
        public required string SelectionReason { get; init; }
    }
}