using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsistenteVirtual.Core.Model
{
    public class ChatMessage
    {
        public string Content { get; set; }
        public bool IsUserMessage { get; set; }
    }

}
