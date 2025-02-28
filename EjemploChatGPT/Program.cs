using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;

//string apiKey = "sk-svcacct-m1DfQMlewRR7KRy1fc-UjwTAElVlN3RbSCOY5bAib2crfFH6CMwrVw0sVIJwJ4pT3BlbkFJhJRm9MdiiEQ9BFenElH6Hq2jsfnwQQYTEp8cbXfz_VYzqE8UhrhuHMFvJ8Nm5AA";
string apiUrl = "http://localhost:5000/api/Consultas/human_Query"; // Asegúrate de que la URL coincide con tu servidor
string? pregunta = "";
bool correr = true;

Console.WriteLine("Chat con API de Base de Datos");

using HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(10) };

while (correr)
{
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
