using System.Text;
using System.Text.Json;

namespace AsistenteVirtual.Core.Service
{
    public class ChatService
    {
        private readonly HttpClient _httpClient;

        public ChatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> SendMessageToApi(string source, string question)
        {
            try
            {
                string jsonRequest = JsonSerializer.Serialize(new { SentenciaNatural = question });
                StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync($"http://localhost:5000/api/Consultas/{source}", content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var parsedResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse);
                    return parsedResponse["answer"];
                }
                else
                {
                    return $"Error API: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"Error en la solicitud: {ex.Message}";
            }
        }
    }
}
