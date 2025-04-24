using Microsoft.SemanticKernel;

namespace RecipeChat.GroupChat.Models
{
    public class GroupChatResponseGeneratedEventArgs : EventArgs
    {
        public ChatMessageContent ChatMessageContent { get; }

        public GroupChatResponseGeneratedEventArgs(ChatMessageContent chatMessageContent)
        {
            ChatMessageContent = chatMessageContent;
        }
    }
}