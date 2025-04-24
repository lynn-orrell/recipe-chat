using Microsoft.Extensions.AI;

namespace RecipeChat.Web.Shared.Models
{
    public class SlimChatMessage
    {
        public ChatRole Role { get; set; }
        public string MessageId { get; set; }
        public string MessageChunk { get; set; }

        public SlimChatMessage(ChatRole role, string messageId, string messageChunk)
        {
            Role = role;
            MessageId = messageId;
            MessageChunk = messageChunk;
        }

        public void AddChunk(string chunk)
        {
            MessageChunk += chunk;
        }
    }
}