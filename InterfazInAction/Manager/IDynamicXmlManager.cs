namespace InterfazInAction.Manager
{
    public interface IDynamicXmlManager
    {
        /// <summary>
        /// Procesa un XML buscando TODOS los procesos configurados para la interfaz dada.
        /// Ejecuta los inserts en una única transacción.
        /// </summary>
        /// <param name="interfaceName">Nombre de la interfaz (ej: "MMI019") que agrupa uno o varios inserts.</param>
        /// <param name="xmlContent">El string con el contenido XML completo</param>
        /// <returns>Número total de registros insertados (suma de todas las tablas)</returns>
        Task<int> ProcessXmlAsync(string interfaceName, string xmlContent);
    }
}
