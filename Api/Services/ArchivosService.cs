using System.Text.Json;
using System.Text;
using Api.Model;

namespace Api.Services
{
    public class ArchivosService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _indexFilePath = "file_index.json"; // Archivo donde guardo el indice


        public ArchivosService(IConfiguration configuration)
        {
            _apiKey = configuration.GetValue<string>("Variables:API_KEY_OPENAI");
            _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
        }

        /// <summary>
        /// Lee todos los archivos y los divide en fragmentos almacenándolos en un índice local.
        /// </summary>
        public void IndexFiles(string folderPath)
        {
            var fileIndex = new List<FileChunk>();

            if (Directory.Exists(folderPath))
            {
                foreach (string file in Directory.GetFiles(folderPath))
                {
                    try
                    {
                        string content = File.ReadAllText(file);
                        var chunks = SplitIntoChunks(content, 1000); // Fragmentos de 1000 caracteres

                        for (int i = 0; i < chunks.Count; i++)
                        {
                            fileIndex.Add(new FileChunk
                            {
                                FileName = Path.GetFileName(file),
                                ChunkIndex = i,
                                Content = chunks[i]
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error al leer el archivo {file}: {ex.Message}");
                    }
                }
            }

            File.WriteAllText(_indexFilePath, JsonSerializer.Serialize(fileIndex, new JsonSerializerOptions { WriteIndented = true }));
        }
        /// <summary>
        /// Divide un texto en fragmentos de tamaño determinado.
        /// </summary>
        private List<string> SplitIntoChunks(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
            }
            return chunks;
        }
        /// <summary>
        /// Busca en el índice y encuentra los fragmentos más relevantes para la pregunta.
        /// </summary>
        public async Task<string> SearchAndAnswerAsync(string userQuestion)
        {
            if (!File.Exists(_indexFilePath))
            {
                return "No hay archivos indexados.";
            }

            var fileChunks = JsonSerializer.Deserialize<List<FileChunk>>(File.ReadAllText(_indexFilePath));
            if (fileChunks == null || fileChunks.Count == 0)
            {
                return "No se encontraron fragmentos en el índice.";
            }

            // Filtrar fragmentos más relevantes basados en la pregunta
            var relevantChunks = fileChunks.Where(f => f.Content.Contains(userQuestion, StringComparison.OrdinalIgnoreCase))
                                           .Take(5) // Limitar la cantidad de fragmentos enviados a OpenAI
                                           .Select(f => f.Content)
                                           .ToList();

            if (relevantChunks.Count == 0)
            {
                return "No se encontraron fragmentos relevantes para la pregunta.";
            }

            string prompt = GeneratePrompt(userQuestion, relevantChunks);
            return await GetChatCompletionAsync(prompt);
        }
        public string GeneratePrompt(string userQuestion, List<string> fileChunks)
        {
            return $@"
            Given the following extracted text from various documents, answer the user's question based on their content.
            Ensure the response is clear and in valid JSON format.

            <documents>
            {string.Join("\n\n", fileChunks)}
            </documents>

            User question: {userQuestion}
            Provide the response in valid JSON format.";
        }

        /// <summary>
        /// Lee el contenido de todos los archivos en una carpeta.
        /// </summary>
        //public List<string> GetFilesContent(string folderPath)
        //{
        //    List<string> fileContents = new List<string>();

        //    if (Directory.Exists(folderPath))
        //    {
        //        foreach (string file in Directory.GetFiles(folderPath))
        //        {
        //            try
        //            {
        //                string content = System.IO.File.ReadAllText(file);
        //                content = content.Length > 500 ? content.Substring(0, 500) + "..." : content; // limite a 500 caracteres por hoja
        //                fileContents.Add($"File: {Path.GetFileName(file)}\nContent:\n{content}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($"Error al leer el archivo {file}: {ex.Message}");
        //            }
        //        }
        //    }

        //    return fileContents;
        //}

        /// <summary>
        /// Genera el prompt para OpenAI con la pregunta y el contenido de los archivos.
        /// </summary>
       

        /// <summary>
        /// Método auxiliar para conectarse a la API de OpenAI.
        /// </summary>
        public async Task<string> GetChatCompletionAsync(string systemMessage)
        {
            string url = "https://api.openai.com/v1/chat/completions";

            var requestBody = new
            {
                model = "gpt-4o",
                response_format = new { type = "json_object" },
                messages = new List<object>
                {
                    new { role = "system", content = "Respond only in JSON format. " + systemMessage }
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestBody);
            using StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            await Task.Delay(1000);
            HttpResponseMessage responseMessage = await _httpClient.PostAsync(url, content);
            string res = await responseMessage.Content.ReadAsStringAsync();

            return res;
        }
    }
    public class FileChunk
    {
        public string FileName { get; set; }
        public int ChunkIndex { get; set; }
        public string Content { get; set; }
    }
}
