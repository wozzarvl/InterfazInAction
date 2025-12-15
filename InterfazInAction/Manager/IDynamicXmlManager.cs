namespace InterfazInAction.Manager
{
    public interface IDynamicXmlManager
    {
        /// <summary>
        /// Procesa un XML basado en la configuración definida en la base de datos.
        /// </summary>
        /// <param name="processName">Nombre del proceso configurado (ej: "SAP_MATERIAL_IMPORT")</param>
        /// <param name="xmlContent">El string con el contenido XML completo</param>
        /// <returns>Número de registros insertados con éxito</returns>
         Task<int> ProcessXmlAsync(string processName, string xmlContent);
    }
}
