using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeChat.Web.Shared.Models
{
    public enum NotificationType
    {
        UserMessage,
        AssistantMessage,
        SpeakerChange,
        AgentGroupChatTerminationUpdate,
        AgentGroupChatComplete
    }

    public class ConversationNotification
    {
        public NotificationType NotificationType { get; set; }
        public string MessageId { get; set; }
        public string Text { get; set; }

        public ConversationNotification(NotificationType notificationType, string messageId, string text)
        {
            NotificationType = notificationType;
            MessageId = messageId;
            Text = text;
        }

        public void AddTextChunk(string chunk)
        {
            Text += chunk;
        }
    }
}