using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using JPS_ClassLibrary.core.Contexto;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using Api.Model;

namespace Api.Services
{
    /// <summary>
    /// Servicio que c entraliza las consultas que hace el GTP a mi base de datos.
    /// </summary>
    public class BdService
    {
        private readonly JPSContexto _contexto;
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        public BdService(JPSContexto contexto, IConfiguration configuration)
        {
            _contexto = contexto;
            _apiKey = configuration.GetValue<string>("Variables:API_KEY_OPENAI");
            _httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
        }

        /// <summary>
        /// Metodo para obtener el esquema de la BD. Conectarme a la bd.
        /// </summary>
        /// <returns></returns>
        private string GetSchema()
        {
            //    var schema = _contexto.Model.GetEntityTypes()
            //.Select(t => new
            //{
            //    TableName = t.GetTableName(),
            //    Columns = t.GetProperties()
            //        .Select((p, index) => new
            //        {
            //            ColumnName = index == 0 ? t.GetTableName() + p.Name : p.Name,
            //            ColumnType = p.ClrType.Name
            //        }).ToList()
            //}).ToList();

            //    return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });

            List<object> schemaList = new List<object>();

            foreach (IEntityType t in _contexto.Model.GetEntityTypes())
            {
                string tableName = t.GetTableName();
                List<object> columns = new List<object>();
                List<IProperty> properties = t.GetProperties().ToList();

                for (int i = 0; i < properties.Count; i++)
                {
                    string columnName = (i == 0) ? tableName + properties[i].Name : properties[i].Name;
                    columns.Add(new
                    {
                        ColumnName = columnName,
                        ColumnType = properties[i].ClrType.Name
                    });
                }

                schemaList.Add(new
                {
                    TableName = tableName,
                    Columns = columns
                });
            }

            return JsonSerializer.Serialize(schemaList, new JsonSerializerOptions { WriteIndented = true });
        }


        /// <summary>
        /// Este metodo recibe una sentencia en lenguaje natural, y devuelve algo equivalente en lenguaje SQL para ser ejecutado en la bd.
        /// Porque, para que la IA pueda hacer las sentencias que recibe en lenguaje natural,
        /// en la bd en lenguaje SQL, primero debo decirle como es mi esquema, etc(la IA desconoce el nombre de mis tablas, columnas, etc).
        /// </summary>
        /// <param name="humanQuery"></param>
        /// <returns></returns>
        public async Task<string> HumanQueryToSql(string humanQuery)
        {
            // obtengo el shcema de mi base de datos ->
            string databaseSchema = GetSchema();

            /*Aclaracion para JPS: que tenga en cuenta que las primary keys son {nombre_tabla}+Id...
            asi:         Keep in mind that primary keys or IDs are prefixed with the table name. For example, the primary key of 'Planta' is 'PlantaId'.*/
            var prompt = $@"
            Given the following database schema, generate an SQL query that retrieves the requested information. 
            The response should be in JSON format with the key ""sql_query"".
            Also, ensure that all column names in the generated query are enclosed in double quotes (\"") to maintain proper SQL syntax.
            Keep in mind that primary keys or IDs are prefixed with the table name. For example, the primary key of 'Planta' is 'PlantaId'.
            <example>
            {{
                ""sql_query"": ""SELECT * FROM users WHERE age > 18;"",
                ""original_query"": ""Show me all users older than 18 years old.""
            }}
            </example>
            <schema>
            {databaseSchema}
            </schema>";


            // envio el esquema completo con la consulta al LLM (al chat gpt propiamente) ->
            string response = await Consultar_OpenAI(prompt, humanQuery);
            return response;
        }
        /// <summary>
        /// Metodo auxiliar para conectarme a la api de openAI
        /// </summary>
        /// <param name="systemMessage">prompt que le paso al LLM</param>
        /// <param name="userMessage">humanQuery. Si viene null, es porque es el segundo llamado a este metodo (el lamado proximo al cliente).</param>
        /// <returns></returns>
        private async Task<string> Consultar_OpenAI(string systemMessage, string? userMessage)
        {
            string url = "https://api.openai.com/v1/chat/completions";

            object requestBody = new object();
            List<object> messages = new List<object> { new { role = "system", content = systemMessage } };

            if (!string.IsNullOrEmpty(userMessage))
            {
                messages.Add(new { role = "user", content = userMessage });
                requestBody = new
                {
                    model = "gpt-4o",
                    response_format = new { type = "json_object" },
                    messages
                };
            }
            else
            {
                requestBody = new
                {
                    model = "gpt-4o",
                    messages
                };
            }

            string jsonRequest = JsonSerializer.Serialize(requestBody);
            using StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            HttpResponseMessage responseMessage = await _httpClient.PostAsync(url, content);


            if (responseMessage.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string res = await responseMessage.Content.ReadAsStringAsync();

                // Deserialización del JSON de OpenAI
                OpenAiResponse openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(res);
                if (openAiResponse?.choices?.Count > 0)
                {
                    string contentJson = openAiResponse.choices[0].message.content;
                    if (!string.IsNullOrEmpty(userMessage))
                    {
                        Dictionary<string, string> queryDict = JsonSerializer.Deserialize<Dictionary<string, string>>(contentJson);
                        if (queryDict != null && queryDict.ContainsKey("sql_query"))
                        {
                            return queryDict["sql_query"];
                        }
                        else
                        {
                            throw new Exception("No se pudo extraer la consulta SQL.");
                        }
                    }
                    else
                    {
                        // aca entraria cuando no hay una human query. Es decir, cuando ya tengo que devolver lo que verá el usuario.
                        return contentJson;
                    }
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

        /// <summary>
        /// Metodo que recibe ya la query devuelta por el LLM, y la manda a la base de datos para obtener info.
        /// </summary>
        /// <param name="sqlQuery"></param>
        /// <returns></returns>
        public async Task<string> QueryDatabase(string sqlQuery)
        {
            try
            {
                DbConnection connection = _contexto.Database.GetDbConnection();
                await connection.OpenAsync();

                using DbCommand command = connection.CreateCommand();
                if (!IsValidSqlQuery(sqlQuery))
                {
                    return JsonSerializer.Serialize(new { error = "Consulta SQL inválida o peligrosa detectada." });
                }

                command.CommandText = sqlQuery;
                command.CommandType = CommandType.Text;



                using DbDataReader reader = await command.ExecuteReaderAsync();

                List<Dictionary<string, object>> resultList = new List<Dictionary<string, object>>();

                while (await reader.ReadAsync())
                {
                    Dictionary<string, object> row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.GetValue(i);
                    }
                    resultList.Add(row);
                }

                return JsonSerializer.Serialize(resultList, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = "Hubo un problema al ejecutar la consulta. Revisa la pregunta o intenta con otra consulta." });
            }
        }
        /// <summary>
        /// Metodo interno para evitar que el GPT ejecute sentencias "maliciosas" en mi base de datos. (que solo pueda hacer consultass GET).
        /// </summary>
        /// <returns></returns>
        private bool IsValidSqlQuery(string sqlQuery)
        {
            string[] forbiddenKeywords = { "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "TRUNCATE" };
            foreach (string keyword in forbiddenKeywords)
            {
                if (sqlQuery.ToUpper().Contains(keyword))
                {
                    return false;
                }
            }
            return true;
        }


        public async Task<string> BuildAnswer(string result, string humanQuery)
        {
            string serializedResult = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = false, // Minimiza el tamaño
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Evita caracteres escapados innecesarios
            });

            // Incluir un ejemplo en el prompt
            string prompt = $@"
            Given a user's question and the SQL rows response from the database, write a response to the user's question.
            Ensure that the response is clear and directly answers the question.

            Example:
            User question: ""How many users are in the database?""
            SQL response: 
            [
                {{ ""count"": 1200 }}
            ]
            Expected answer: ""There are 1,200 users in the database.""

            User question: ""{humanQuery}""
            SQL response: 
            {serializedResult}
            ";


            // envio el esquema completo con la consulta al LLM (al chat gpt propiamente) ->
            string response = await Consultar_OpenAI(prompt, null);
            return response;
        }

    }
    public class PostHumanQueryResponse
    {
        public List<Dictionary<string, object>> Result { get; set; } = new();
    }
}
