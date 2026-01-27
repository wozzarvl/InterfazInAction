using InterfazInAction.Models;

namespace InterfazInAction.Manager
{
    public interface IConfigurationManager
    {
        Task<List<integrationProcess>> GetConfigurations();
    }
}
