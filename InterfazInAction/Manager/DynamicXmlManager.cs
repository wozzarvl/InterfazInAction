using InterfazInAction.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Xml.Linq;
using System.Xml.XPath;

namespace InterfazInAction.Manager
{
    public class DynamicXmlManager : IDynamicXmlManager
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public DynamicXmlManager(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<int> ProcessXmlAsync(string processName, string xmlContent)
        {
            // 1. CARGAR CONFIGURACIÓN DESDE LA BASE DE DATOS
            // Buscamos el proceso y sus campos asociados
            var processConfig = await _context.IntegrationProcesses
                .Include(p => p.Fields)
                .FirstOrDefaultAsync(p => p.ProcessName == processName);

            if (processConfig == null)
                throw new Exception($"El proceso de integración '{processName}' no está configurado en la base de datos.");

            if (processConfig.Fields == null || !processConfig.Fields.Any())
                throw new Exception($"El proceso '{processName}' no tiene campos mapeados.");

            // 2. PARSEAR EL XML
            var xDoc = XDocument.Parse(xmlContent);

            // 3. SELECCIONAR NODOS A ITERAR
            // Usamos el XPath definido en BD (ej. "//*[local-name()='DT_MaterialesDetalleSAP']")
            var nodes = xDoc.XPathSelectElements(processConfig.XmlIterator).ToList();

            if (!nodes.Any()) return 0; // No se encontraron datos para procesar

            int insertedCount = 0;
            var connectionString = _configuration.GetConnectionString("WmsConnection");

            // 4. PREPARAR CONEXIÓN RAW A POSTGRES (ADO.NET)
            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // Usamos una transacción para garantizar integridad: O se inserta todo el lote o nada.
                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        foreach (var node in nodes)
                        {
                            // Listas para construir el query dinámico
                            var colNames = new List<string>();
                            var paramNames = new List<string>();

                            var cmd = new NpgsqlCommand();
                            cmd.Connection = conn;
                            cmd.Transaction = transaction;

                            // Recorremos cada campo configurado en la tabla IntegrationFields
                            foreach (var field in processConfig.Fields)
                            {
                                object dbVal = null;

                                // --- LÓGICA DE OBTENCIÓN DE VALOR ---

                                // CASO A: Valor de Sistema (ej. Fecha Actual)
                                if (field.DataType == "CURRENT_TIMESTAMP")
                                {
                                    dbVal = DateTime.Now; //DateTime.UtcNow; // O DateTime.Now según tu zona horaria
                                }
                                // CASO B: Valor Fijo (XmlPath es nulo)
                                else if (string.IsNullOrEmpty(field.XmlPath))
                                {
                                    dbVal = ConvertValue(field.DefaultValue, field.DataType);
                                }
                                // CASO C: Valor proveniente del XML
                                else
                                {
                                    string valStr = GetValueFromXml(node, field.XmlPath);

                                    // Si viene vacío del XML, intentamos usar el DefaultValue
                                    if (string.IsNullOrEmpty(valStr) && !string.IsNullOrEmpty(field.DefaultValue))
                                    {
                                        valStr = field.DefaultValue;
                                    }

                                    dbVal = ConvertValue(valStr, field.DataType);
                                }

                                // --- AGREGAR AL COMANDO SQL ---
                                // Solo agregamos columnas si tenemos un valor (o si quieres permitir nulos explícitos)
                                if (dbVal != null)
                                {
                                    //Validacion de que si el campo hace referencia a otra tabla  haga el insert para que no de error de llave foranea
                                    if (!string.IsNullOrEmpty(field.ReferenceTable) && !string.IsNullOrEmpty(field.ReferenceColumn))
                                    {
                                        // Truco de PostgreSQL: INSERT ... ON CONFLICT DO NOTHING
                                        // Esto asume que ReferenceColumn tiene una restricción UNIQUE en la tabla destino
                                        string depSql = $@"
                                                        INSERT INTO {field.ReferenceTable} (""{field.ReferenceColumn}"",created_at,updated_at) 
                                                        VALUES (@val,@valcreated_at,@valupdated_at) 
                                                        ON CONFLICT (""{field.ReferenceColumn}"") DO NOTHING";

                                        var depCmd = new NpgsqlCommand(depSql, conn, transaction);
                                        depCmd.Parameters.AddWithValue("@val", dbVal);
                                        depCmd.Parameters.AddWithValue("@valcreated_at", DateTime.Now);
                                        depCmd.Parameters.AddWithValue("@valupdated_at", DateTime.Now);
                                        await depCmd.ExecuteNonQueryAsync();
                                    }


                                    // Agregamos comillas al nombre de la columna para proteger mayúsculas/minúsculas en Postgres
                                    colNames.Add($"\"{field.DbColumn}\"");

                                    string pName = $"@p{paramNames.Count}"; // Nombre del parámetro: @p0, @p1...
                                    paramNames.Add(pName);

                                    cmd.Parameters.AddWithValue(pName, dbVal);
                                }
                            }

                            // 5. EJECUTAR INSERT SI HAY DATOS
                            if (colNames.Count > 0)
                            {
                                // Construcción del SQL: INSERT INTO tabla (col1, col2) VALUES (@p0, @p1)
                                string sql = $"INSERT INTO {processConfig.TargetTable} ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";

                                // Opcional: Manejo de conflictos (Upsert) si ya existe el código
                                // sql += " ON CONFLICT (code) DO NOTHING"; 

                                cmd.CommandText = sql;
                                await cmd.ExecuteNonQueryAsync();
                                insertedCount++;
                            }
                        }

                        // Si todo sale bien, confirmamos los cambios en la BD
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        // Si algo falla, revertimos todo para no dejar datos corruptos
                        await transaction.RollbackAsync();
                        throw; // Re-lanzamos el error para que lo vea el Controller
                    }
                }
            }

            return insertedCount;
        }

        // --- MÉTODOS AUXILIARES ---

        /// <summary>
        /// Busca un valor en el nodo XML, soportando rutas complejas (ej "Header/Date") 
        /// e ignorando Namespaces (n0:)
        /// </summary>
        private string GetValueFromXml(XElement parentNode, string path)
        {
            if (parentNode == null || string.IsNullOrEmpty(path)) return null;

            XElement current = parentNode;
            var parts = path.Split('/'); // Soporte para niveles: "DT_UM/MEINH"

            foreach (var part in parts)
            {
                if (current == null) return null;

                // Buscamos el hijo ignorando el namespace (n0:MATNR == MATNR)
                current = current.Elements()
                    .FirstOrDefault(e => e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));
            }

            return current?.Value;
        }

        /// <summary>
        /// Convierte el string del XML al tipo de dato real de C#/Postgres
        /// </summary>
        private object ConvertValue(string val, string type)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
            if (string.IsNullOrEmpty(type)) return val; // Si no hay tipo, se va como string

            try
            {
                return type.ToLower() switch
                {
                    "int" => int.Parse(val),
                    // Decimal: Importante manejar puntos vs comas según cultura
                    "decimal" => decimal.Parse(val, System.Globalization.CultureInfo.InvariantCulture),
                    "float" => float.Parse(val, System.Globalization.CultureInfo.InvariantCulture),
                    "boolean" => (val == "1" || val.ToUpper() == "TRUE" || val.ToUpper() == "X"), // SAP usa "X" a veces
                    "datetime" => DateTime.Parse(val),
                    _ => val // Por defecto string
                };
            }
            catch
            {
                // Si falla la conversión (ej: fecha inválida), retornamos null o lanzamos error según prefieras
                return DBNull.Value;
            }
        }
    }
}