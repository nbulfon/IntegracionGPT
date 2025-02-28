using Api.Services;

namespace Api.Model
{
    /// <summary>
    /// Objeto que devuelve OpenAI en la response POST sobre una consulta común.
    /// </summary>
    public class OpenAiResponse
    {
        public List<Choice> choices { get; set; }
    }
    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string content { get; set; }
    }
}
