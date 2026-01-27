using InterfazInAction.Models;
using Microsoft.AspNetCore.Mvc;
using InterfazInAction.Manager;

namespace InterfazInAction.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly Manager.IConfigurationManager _configurationManager;

        public ConfigurationController(Manager.IConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
        }

        [HttpGet("GetConfigurations")]
        public IActionResult GetConfigurations()
        {
            List<integrationProcess> lista= _configurationManager.GetConfigurations().GetAwaiter().GetResult();
            return   Ok(ApiResponse<List<integrationProcess>>.Ok(lista));
        }
    }
}
