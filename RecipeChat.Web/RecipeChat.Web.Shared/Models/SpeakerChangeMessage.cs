using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeChat.Web.Shared.Models
{
    public class SpeakerChangeMessage
    {
        public string Speaker { get; set; }

        public SpeakerChangeMessage(string speaker)
        {
            Speaker = speaker;
        }
    }
}