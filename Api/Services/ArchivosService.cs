using System.Text.Json;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using ClosedXML.Excel;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Api.Model;

namespace Api.Services
{
    public class ArchivosService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly int _porcentajeCaracteresAGuardarFile;
         // Ruta del índice de archivos

        public ArchivosService(IConfiguration configuration)
        {
            _apiKey = configuration.GetValue<string>("Variables:API_KEY_OPENAI");
            _porcentajeCaracteresAGuardarFile = ( configuration.GetValue<int>("Variables:PORCENTAJE_CARACTERES_FILE_A_GUARDAR") ) /100;
            _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
        }

        #region Index json

        /// <summary>
        /// Indexa archivos PDF, Word y Excel, dividiéndolos en fragmentos manejables.
        /// </summary>
        public void IndexarArchivos(string folderPath, string indexFilePath)
        {
            List<FragmentoArchivo> fileIndex = new List<FragmentoArchivo>();

            // Verifica si el directorio existe
            if (Directory.Exists(folderPath))
            {
                foreach (string fileName in Directory.GetFiles(folderPath))
                {
                    fileIndex = ProcesarArchivosEnIndexJson(fileIndex,fileName);
                }
            }

            // Guarda el índice en un archivo JSON
            File.WriteAllText(indexFilePath, JsonSerializer.Serialize(fileIndex, new JsonSerializerOptions { WriteIndented = true }));
        }
        private List<FragmentoArchivo> ProcesarArchivosEnIndexJson(List<FragmentoArchivo> fileIndex,string file)
        {
            try
            {
                // Extrae el texto del archivo (PDF, Word o Excel)
                string content = ExtraerTextoDeUnFile(file);

                // saco el 60% del total del archivo
                int caracteresAGuardar = (int)(content.Length * _porcentajeCaracteresAGuardarFile);

                List<string> fragmentos = SepararEnFragmentos(content, caracteresAGuardar);

                // Guarda cada fragmento en la lista de índice
                for (int i = 0; i < fragmentos.Count; i++)
                {
                    fileIndex.Add(new FragmentoArchivo
                    {
                        NombreArchivo = System.IO.Path.GetFileName(file),
                        IndiceFragmento = i,
                        Contenido = fragmentos[i]
                    });
                }

                return fileIndex;
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Error al leer el archivo {file}: {ex.Message}");
                throw new Exception($"Error al leer el archivo {file}: {ex.Message}");
            }
        }
        /// <summary>
        /// Agrega nuevos archivos al índice existente sin eliminar los anteriores.
        /// </summary>
        public void AgregarArchivosAlIndex(string folderPath, string indexFilePath, List<string> nuevosArchivos)
        {
            try
            {
                // Cargar índice actual
                List<FragmentoArchivo> fileIndex = new List<FragmentoArchivo>();

                if (System.IO.File.Exists(indexFilePath))
                {
                    string json = System.IO.File.ReadAllText(indexFilePath);
                    fileIndex = (JsonSerializer.Deserialize<List<FragmentoArchivo>>(json) != null) ? JsonSerializer.Deserialize<List<FragmentoArchivo>>(json) : new List<FragmentoArchivo>();
                }

                // Procesar nuevos archivos y añadirlos al índice
                foreach (string fileName in nuevosArchivos)
                {
                    fileIndex = ProcesarArchivosEnIndexJson(fileIndex,fileName);
                }

                // Guardar el índice actualizado
                System.IO.File.WriteAllText(indexFilePath, JsonSerializer.Serialize(fileIndex, new JsonSerializerOptions { WriteIndented = true }));

                Console.WriteLine($"Se añadieron {nuevosArchivos.Count} archivos al índice.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar el índice: {ex.Message}");
            }
        }

        #endregion

        #region Extraer texto

        /// <summary>
        /// Determina el tipo de archivo y extrae su contenido de manera apropiada.
        /// </summary>
        private string ExtraerTextoDeUnFile(string filePath)
        {
            string extension = System.IO.Path.GetExtension(filePath).ToLower();
            if (extension == ".pdf") return ExtraerTextoDe_Pdf(filePath);
            if (extension == ".docx") return ExtraerTextoDe_World(filePath);
            if (extension == ".xlsx") return ExtraerTextoDe_Excel(filePath);
            return "";
        }

        /// <summary>
        /// Extrae el texto de un archivo PDF usando iTextSharp.
        /// </summary>
        private string ExtraerTextoDe_Pdf(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using PdfReader reader = new PdfReader(filePath);
            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
            }
            return text.ToString();
        }

        /// <summary>
        /// Extrae el texto de un archivo Word (.docx) usando OpenXML.
        /// </summary>
        private string ExtraerTextoDe_World(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false);
            foreach (var para in doc.MainDocumentPart.Document.Body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            {
                text.AppendLine(para.InnerText);
            }
            return text.ToString();
        }

        /// <summary>
        /// Extrae el texto de un archivo Excel (.xlsx) usando ClosedXML.
        /// </summary>
        private string ExtraerTextoDe_Excel(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using var workbook = new XLWorkbook(filePath);
            foreach (var sheet in workbook.Worksheets)
            {
                foreach (var row in sheet.RowsUsed())
                {
                    // Combina todas las celdas de la fila en un solo string separado por "|"
                    text.AppendLine(string.Join(" | ", row.Cells().Select(c => c.Value.ToString())));
                }
            }
            return text.ToString();
        }

        #endregion

        /// <summary>
        /// Divide un texto en fragmentos de tamaño determinado para facilitar su procesamiento.
        /// </summary>
        private List<string> SepararEnFragmentos(string text, int cantidadCaracteres)
        {
            List<string> fragmentos = new List<string>();
            for (int i = 0; i < text.Length; i += cantidadCaracteres)
            {
                fragmentos.Add(text.Substring(i, Math.Min(cantidadCaracteres, text.Length - i)));
            }
            return fragmentos;
        }

        /// <summary>
        /// Busca en el índice de archivos y encuentra los fragmentos más relevantes para la pregunta del usuario.
        /// </summary>
        public async Task<string> BuscarFragmentosMasRelevantes(string userQuestion, string indexFilePath)
        {
            if (!File.Exists(indexFilePath))
            {
                return "No hay archivos indexados.";
            }

            // carga el índice desde el archivo JSON
            List<FragmentoArchivo> listFragmentoArchivo = JsonSerializer.Deserialize<List<FragmentoArchivo>>(File.ReadAllText(indexFilePath));
            if (listFragmentoArchivo == null || listFragmentoArchivo.Count == 0)
            {
                return "No se encontraron fragmentos en el índice.";
            }

            // divide la pregunta del usuario en palabras clave para búsqueda parcial
            List<string> palabrasClave = userQuestion.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(k => k.ToLower())
                                       .ToList();

            // filtra los fragmentos más relevantes que contienen palabras clave de la pregunta ->
            int fragmentCount = userQuestion.Length > 50 ? 7 : 5;
            List<string> fragmentosRelevantes = listFragmentoArchivo
                .Where(f => palabrasClave.Any(k => f.Contenido.ToLower().Contains(k)))
                .Take(fragmentCount) // usa el valor dinámico
                .Select(f => f.Contenido)
                .ToList();

            if (fragmentosRelevantes.Count == 0)
            {
                return "No se encontraron fragmentos relevantes para la pregunta.";
            }

            // genera el prompt con la pregunta del usuario y los fragmentos de archivos encontrados ->
            string prompt = GenerarPrompt(userQuestion, fragmentosRelevantes);
            return await Consultar_OpenAI(prompt);
        }

        /// <summary>
        /// Genera el prompt para OpenAI con la pregunta y los fragmentos relevantes de los documentos.
        /// </summary>
        private string GenerarPrompt(string userQuestion, List<string> fragmentos)
        {
            return $@"
            A partir de los siguientes fragmentos de documentos (PDFs, Word, Excel), responde a la pregunta del usuario.
            Asegúrate de que la respuesta esté en español y sea clara.

            <documentos>
            {string.Join("\n\n", fragmentos)}
            </documentos>

            Pregunta del usuario: {userQuestion}
            Proporciona la respuesta en **español** en formato JSON.";
        }

        /// <summary>
        /// Conecta con OpenAI y obtiene la respuesta basada en los fragmentos de archivos seleccionados.
        /// </summary>
        private async Task<string> Consultar_OpenAI(string systemMessage)
        {
            string url = "https://api.openai.com/v1/chat/completions";

            var requestBody = new
            {
                model = "gpt-4o",
                response_format = new { type = "json_object" },
                messages = new List<object>
                {
                    new { role = "system", content = "Responde siempre en español. " + systemMessage }
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestBody);
            using StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            await Task.Delay(1000); // Espera un segundo para evitar sobrecarga en OpenAI
            HttpResponseMessage responseMessage = await _httpClient.PostAsync(url, content);

            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string res = await responseMessage.Content.ReadAsStringAsync();

                // Deserialización del JSON de OpenAI
                OpenAiResponse openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(res);
                if (openAiResponse?.choices?.Count > 0)
                {
                    string contentJson = openAiResponse.choices[0].message.content;
                    return contentJson;
                }
                else
                {
                    throw new Exception("No se pudo extraer la consulta SQL.");
                }
            }
            else
            {
                throw new Exception("Problemas con la api de OpenAI");
            }
        }
    }

    /// <summary>
    /// Representa un fragmento de un archivo indexado.
    /// </summary>
    public class FragmentoArchivo
    {
        public string NombreArchivo { get; set; }  // Nombre del archivo original
        public int IndiceFragmento { get; set; }   // Índice del fragmento dentro del archivo
        public string Contenido { get; set; }   // Contenido del fragmento de texto
    }
}
