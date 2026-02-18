using InterfazInAction.Manager;
using InterfazInAction.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;

namespace InterfazInAction.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class integrationController : ControllerBase
    {
        private readonly IDynamicXmlManager _xmlManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public integrationController(IDynamicXmlManager xmlManager,IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _xmlManager = xmlManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;

        }

        /// <summary>
        /// Recibe un XML y ejecuta TODOS los procesos configurados para la interfaz especificada.
        /// (Ej: Inserta en Items y en SKUs bajo una misma transacción)
        /// </summary>
        /// <param name="interfaceName">El nombre de la interfaz (ej: MMI019)</param>
        /// <returns>Total de registros insertados en todas las tablas</returns>
        [HttpPost("{interfaceName}")]
        [Authorize]
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

                int totalRows = await _xmlManager.ProcessXmlAsyncNoUpsert(interfaceName, xmlContent); //_xmlManager.ProcessXmlAsync(interfaceName, xmlContent);

                /* return Ok(new
                 {
                     status = "Success",
                     interfaceName = interfaceName,
                     totalRowsInserted = totalRows,
                     timestamp = DateTime.UtcNow
                 });*/

                var resultData = new IntegrationResult
                {
                    InterfaceName = interfaceName,
                    TotalRowsInserted = totalRows,
                    Timestamp = DateTime.UtcNow
                };
                return Ok(ApiResponse<IntegrationResult>.Ok(resultData));

            }
            catch (Exception ex)
            {
                /* return BadRequest(new
                 {
                     status = "Error",
                     message = ex.Message,
                     // innerException = ex.InnerException?.Message 
                 });*/

                return BadRequest(ApiResponse<IntegrationResult>.Error(ex.Message));
            }
        }

        /// <summary>
        /// Proceso de SALIDA (Outbound): DB -> XML -> ERP
        /// Recibe una lista de IDs, genera los XMLs y los envía al Restlet del ERP.
        /// </summary>
        [HttpPost("outbound/{interfaceName}")]
        [Authorize]
        public async Task<IActionResult> SendOutboundXml(string interfaceName, [FromBody] OutboundRequest request)
        {
            if (request == null || request.RecordIds == null || !request.RecordIds.Any())
                return BadRequest(ApiResponse<string>.Error("La lista de IDs no puede estar vacía."));

            var results = new List<object>();
            int successCount = 0;
            int errorCount = 0;

            try
            {
                // 1. Obtener la URL del ERP desde appsettings.json
                // Formato sugerido en appsettings: "ErpUrls": { "MMI014": "https://netsuite..." }
                string erpUrl = _configuration[$"ErpUrls:{interfaceName}"];

                // Si no hay URL específica, usa una default o lanza error
                if (string.IsNullOrEmpty(erpUrl))
                    erpUrl = _configuration["ErpUrls:Default"];

                if (string.IsNullOrEmpty(erpUrl))
                    return BadRequest(ApiResponse<string>.Error($"No se configuró una URL ERP para '{interfaceName}'."));

                // 2. Generar los XMLs usando el Manager (Paso 4)
                // Devuelve un Diccionario: { IdRegistro : XmlString }
                var generatedXmls = await _xmlManager.CreateOutboundXmlsAsync(interfaceName, request.RecordIds);

                // 3. Enviar cada XML al ERP
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);

                foreach (var item in generatedXmls)
                {
                    int recordId = item.Key;
                    string xmlContent = item.Value;
                    return Ok(xmlContent);
                    try
                    {
                        // Preparar el contenido
                        var httpContent = new StringContent(xmlContent, Encoding.UTF8, "application/xml");

                        // Enviar POST
                        var response = await httpClient.PostAsync(erpUrl, httpContent);

                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                            results.Add(new { Id = recordId, Status = "Sent", Message = "OK" });

                            // TODO: Aquí podrías llamar a un método _xmlManager.MarkAsSent(recordId) 
                            // para actualizar el estatus en tu BD local a 'ENVIADO'.
                        }
                        else
                        {
                            errorCount++;
                            string errorMsg = await response.Content.ReadAsStringAsync();
                            results.Add(new { Id = recordId, Status = "Error", Message = $"ERP Error {response.StatusCode}: {errorMsg}" });
                            Console.WriteLine($"Error enviando ID {recordId}: {errorMsg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        results.Add(new { Id = recordId, Status = "Exception", Message = ex.Message });
                    }
                }

                /*
                // 4. Retornar resumen
                var finalResult = new
                {
                    Interface = interfaceName,
                    TotalProcessed = generatedXmls.Count,
                    Success = successCount,
                    Errors = errorCount,
                    Details = results
                };

                return Ok(ApiResponse<object>.Ok(finalResult));
                */
                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<string>.Error($"Error crítico en proceso outbound: {ex.Message}"));
            }
        }
    }
}