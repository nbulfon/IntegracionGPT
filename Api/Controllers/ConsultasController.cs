using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Services;
using JPS_ClassLibrary.core.Contexto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

namespace Api.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class ConsultasController : ControllerBase
    {
        private readonly ConsultasService _consultasService;
        private readonly JPSContexto _contexto;
        private readonly IConfiguration _configuration;

        public ConsultasController(JPSContexto contexto, IConfiguration configuration)
        {
            _contexto = contexto;
            _configuration = configuration;
            _consultasService = new ConsultasService(_contexto, _configuration);
        }

        /// <summary>
        /// Gets a natural language query, internally transforms it to a SQL query, queries the database, and returns the result.
        /// </summary>
        /// <returns></returns>
        [HttpPost("human_Query")]
        public async Task<IActionResult> HumanQuery([FromBody] QueryStringRecibida queryString)
        {
            try
            {
                /* ej -> que cantidad de Informes hay en mi base de datos ?
                */

                // Transforma la pregunta a sentencia SQL
                string sqlQuery = await _consultasService.HumanQueryToSql(queryString.SentenciaNatural);
                if (string.IsNullOrEmpty(sqlQuery))
                {
                    return BadRequest(new { error = "Falló la generación de la consulta SQL" });
                }

                // Hace la consulta a la base de datos.
                // ejemplo -> SELECT * FROM USUARIO WHERE MAIL = ...
                string result = await _consultasService.QueryDatabase(sqlQuery);

                string answer = await _consultasService.BuildAnswer(result, queryString.SentenciaNatural);

                if (string.IsNullOrEmpty(answer))
                {
                    return BadRequest(new { error = "Falló la generación de la respuesta" });
                }

                return Ok(new { answer });
            }

            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
