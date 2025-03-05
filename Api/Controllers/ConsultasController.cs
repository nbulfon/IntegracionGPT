using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Api.Services;
using JPS_ClassLibrary.core.Contexto;
using Microsoft.AspNetCore.Mvc;
using Api.Model;

namespace Api.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class ConsultasController : ControllerBase
    {
        private readonly BdService _baseDeDatosService;
        private readonly ArchivosService _archivosService;
        private readonly JPSContexto _contexto;
        private readonly string _folderPath;
        private readonly IConfiguration _configuration;
        private readonly string _indexFilePath;

        public ConsultasController(JPSContexto contexto, IConfiguration configuration)
        {
            _contexto = contexto;
            _configuration = configuration;
            _baseDeDatosService = new BdService(_contexto, _configuration);
            _archivosService = new ArchivosService(_configuration);
            _folderPath = configuration.GetValue<string>("Variables:PATH_CARPETA_ARCHIVOS");
            _indexFilePath = configuration.GetValue<string>("Variables:INDEX_FILE_PATH") + "file_index.json";
        }

        /// <summary>
        /// Gets a natural language query, internally transforms it to a SQL query, queries the database, and returns the result.
        /// </summary>
        /// <returns></returns>
        [HttpPost("consultarBaseDeDatos")]
        public async Task<IActionResult> ConsultarBaseDeDatos([FromBody] QueryStringRecibida queryString)
        {
            try
            {
                /* ej -> que cantidad de Informes hay en mi base de datos ?
                */

                // 1. Transforma la pregunta del usuario a una sentencia SQL ->
                string sqlQuery = await _baseDeDatosService.HumanQueryToSql(queryString.SentenciaNatural);
                if (string.IsNullOrEmpty(sqlQuery))
                {
                    return BadRequest(new { error = "Falló la generación de la consulta SQL" });
                }

                // 2. Con la sentencia SQL, hago la consulta a la base de datos ->
                // ejemplo -> SELECT * FROM USUARIO WHERE MAIL = ...
                string result = await _baseDeDatosService.QueryDatabase(sqlQuery);

                // 3. Con la respuesta desde la base de datos, armo una respuesta para mandarle armado al usuario ->
                string answer = await _baseDeDatosService.BuildAnswer(result, queryString.SentenciaNatural);

                if (string.IsNullOrEmpty(answer))
                {
                    return BadRequest(new { error = "Falló la generación de la respuesta" });
                }

                // devuelvo response al usuario ->
                return Ok(new { answer });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Endpoint para buscar en los archivos indexados y responder la pregunta del usuario.
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        [HttpPost("leerArchivos")]
        public async Task<IActionResult> ConsultarArchivos([FromBody] QueryStringRecibida queryString)
        {
            try
            {
                // Verifica si el índice ya existe o si necesita ser reindexado
                VerificarOReindexarArchivos();

                if (string.IsNullOrEmpty(queryString.SentenciaNatural))
                {
                    return BadRequest(new { error = "La pregunta no puede estar vacía." });
                }

                // Buscar en los archivos indexados y obtener respuesta
                string response = await _archivosService.SearchAndAnswerAsync(queryString.SentenciaNatural, _indexFilePath);

                return Ok(new { answer = response });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Error en la consulta: {ex.Message}" });
            }
        }
        /// <summary>
        /// Verifica si hay un índice existente o si es necesario reindexar los archivos.
        /// </summary>
        private void VerificarOReindexarArchivos()
        {
            if (!System.IO.File.Exists(_indexFilePath))
            {
                Console.WriteLine("No se encontró el índice. Indexando archivos...");
                _archivosService.IndexFiles(_folderPath, _indexFilePath);
                return;
            }

            try
            {
                // cargo índice existente
                List<FragmentoArchivo> existingIndex = new List<FragmentoArchivo>();
                if (JsonSerializer.Deserialize<List<FragmentoArchivo>>(System.IO.File.ReadAllText(_indexFilePath)) != null)
                {
                    existingIndex = JsonSerializer.Deserialize<List<FragmentoArchivo>>(System.IO.File.ReadAllText(_indexFilePath));
                }

                // obtengo la lista de archivos actuales en la carpeta
                HashSet<string> archivosEnCarpeta = new HashSet<string>(Directory.GetFiles(_folderPath).Select(Path.GetFileName));
                HashSet<string> archivosIndexados = new HashSet<string>(existingIndex.Select(f => f.NombreArchivo));

                // si hay archivos nuevos que no están en el índice, hago una re-indexacion
                List<string> archivosNuevos = archivosEnCarpeta.Except(archivosIndexados).ToList();
                if (archivosNuevos.Count > 0)
                {
                    _archivosService.AgregarArchivosAlIndex(_folderPath, _indexFilePath, archivosNuevos);
                }

                // Si hay archivos eliminados, se quitan del índice
                List<string> archivosEliminados = archivosIndexados.Except(archivosEnCarpeta).ToList();
                if (archivosEliminados.Count > 0)
                {
                    existingIndex.RemoveAll(f => archivosEliminados.Contains(f.NombreArchivo));
                    System.IO.File.WriteAllText(_indexFilePath, JsonSerializer.Serialize(existingIndex, new JsonSerializerOptions { WriteIndented = true }));
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al leer el índice. Se requiere reindexación completa. Detalles: {ex.Message}");
                //_archivosService.IndexFiles(_folderPath, _indexFilePath);
            }
        }

    }
}
