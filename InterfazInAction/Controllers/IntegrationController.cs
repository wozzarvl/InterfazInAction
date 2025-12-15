using InterfazInAction.Manager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterfazInAction.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IntegrationController : ControllerBase
    {
        private readonly IDynamicXmlManager _xmlManager;

        public IntegrationController(IDynamicXmlManager xmlManager)
        {
            _xmlManager = xmlManager;
        }

        /// <summary>
        /// Recibe un XML genérico y lo procesa según el nombre del proceso configurado en BD.
        /// </summary>
        /// <param name="processName">El nombre del proceso (ej: SAP_MATERIAL_ITEM)</param>
        /// <returns>Resultado de la inserción</returns>
        [HttpPost("{processName}")]
        [Authorize]
        public async Task<IActionResult> ReceiveXml(string processName)
        {
            // 1. Leer el XML crudo directamente del cuerpo de la petición (Request Body)
            // Hacemos esto para evitar problemas de formateo automático de ASP.NET
            using var reader = new StreamReader(Request.Body);
            var xmlContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return BadRequest(new { message = "El cuerpo de la petición (XML) está vacío." });
            }

            try
            {
                // 2. Llamar al Manager Dinámico
                int rowsAffected = await _xmlManager.ProcessXmlAsync(processName, xmlContent);

                return Ok(new
                {
                    status = "Success",
                    process = processName,
                    rowsInserted = rowsAffected,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // Si falla (configuración no encontrada, XML malformado, error SQL), devolvemos 400
                return BadRequest(new
                {
                    status = "Error",
                    message = ex.Message,
                    // innerException = ex.InnerException?.Message // Descomentar para depurar si es necesario
                });
            }
        }
    }
}