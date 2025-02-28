using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Libreria.Core
{
    public class ArchivosService
    {
        private readonly string _folderPath;

        public ArchivosService(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("El directorio no puede ser nulo o vacío", nameof(folderPath));

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"El directorio especificado no existe: {folderPath}");

            _folderPath = folderPath;
        }

        public List<string> GetPdfFiles()
        {
            try
            {
                var pdfFiles = Directory.GetFiles(_folderPath, "*.pdf");
                return new List<string>(pdfFiles);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener archivos PDF: {ex.Message}", ex);
            }
        }
    }
}
