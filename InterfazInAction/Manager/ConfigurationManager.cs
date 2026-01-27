using InterfazInAction.Data;
using InterfazInAction.Models;
using Microsoft.EntityFrameworkCore;

namespace InterfazInAction.Manager
{
    public class ConfigurationManager: IConfigurationManager
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public ConfigurationManager(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }



        public async Task< List<integrationProcess>> GetConfigurations()
        {

            var procesos =await  _context.integrationProcesses.Include(p=>p.Fields).ToListAsync();

            foreach (var process in procesos)
            {
                List<integrationField> fields = new List<integrationField>();
                var campos = await _context.integrationFields.Where(p=>p.ProcessName.Equals(process.ProcessName)).ToListAsync();
                fields = campos;
                process.Fields = fields;
            }
                
            
            return procesos;
        }
    }
}
