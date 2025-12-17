using InterfazInAction.Manager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InterfazInAction.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class IntegrationController : ControllerBase
    {
        private readonly IDynamicXmlManager _xmlManager;

        public IntegrationController(IDynamicXmlManager xmlManager)
        {
            _xmlManager = xmlManager;
        }

        /// <summary>
        /// Recibe un XML y ejecuta TODOS los procesos configurados para la interfaz especificada.
        /// (Ej: Inserta en Items y en SKUs bajo una misma transacción)
        /// </summary>
        /// <param name="interfaceName">El nombre de la interfaz (ej: MMI019)</param>
        /// <returns>Total de registros insertados en todas las tablas</returns>
        [HttpPost("{interfaceName}")]
        public async Task<IActionResult> ReceiveXml(string interfaceName)
        {
         
            using var reader = new StreamReader(Request.Body);
            var xmlContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return BadRequest(new { message = "El cuerpo de la petición (XML) está vacío." });
            }

            try
            {
         
                int totalRows = await _xmlManager.ProcessXmlAsync(interfaceName, xmlContent);

                return Ok(new
                {
                    status = "Success",
                    interfaceName = interfaceName,
                    totalRowsInserted = totalRows,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = ex.Message,
                    // innerException = ex.InnerException?.Message 
                });
            }
        }
    }
}