using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

string apiUrl = "http://localhost:5000/api/Consultas"; // Asegúrate de que la URL coincide con tu servidor
string? pregunta = "";
bool correr = true;
string? origenDatos = "";

Console.WriteLine("Chat con API de Base de Datos");

// Preguntar al usuario de dónde quiere obtener la información
while (true)
{
    Console.Write("¿De donde queres obtener la información? (Escribir 'de la base' o 'de mis archivos'): ");
    origenDatos = Console.ReadLine()?.Trim().ToLower();

    if (origenDatos == "de la base" || origenDatos == "de la base de datos")
    {
        apiUrl += "/consultarBaseDeDatos";
        break;
    }
    else if (origenDatos == "de mis archivos" || origenDatos == "de los archivos" || origenDatos == "de archivos" || origenDatos == "archivos")
    {
        apiUrl += "/leer_archivos";
        break;
    }
    else
    {
        Console.WriteLine("Opción no válida. Inténtalo nuevamente.");
    }
}

using HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(2) };
while (correr)
{
    Console.Write("Haceme una pregunta!");
    Console.Write(">> ");
    pregunta = Console.ReadLine();

    if (pregunta == "q")
    {
        correr = false;
        break;
    }

    try
    {
        string jsonRequest = JsonSerializer.Serialize(new { SentenciaNatural = pregunta });
        StringContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);
        string jsonResponse = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var parsedResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse);
            Console.WriteLine($"\nRespuesta >> {parsedResponse["answer"]}\n");
        }
        else
        {
            Console.WriteLine($"\nError API ({response.StatusCode}) >> {jsonResponse}\n");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError en la solicitud: {ex.Message}\n");
    }
}

Console.WriteLine("\nPresione cualquier tecla para salir...");
Console.ReadKey();
