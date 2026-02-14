using InterfazInAction.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Diagnostics;
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

        public async Task<int> ProcessXmlAsync(string interfaceName, string xmlContent)
        {
            
            var configs = await _context.integrationProcesses
                .Include(p => p.Fields)
                .Where(p => p.InterfaceName == interfaceName)
                .OrderBy(p => p.Order)
                .ToListAsync();

            if (!configs.Any())
                throw new Exception($"No se encontraron procesos configurados para la interfaz '{interfaceName}'.");

         
            var xDoc = XDocument.Parse(xmlContent);

            int totalInserted = 0;

           
            var connectionString = _configuration.GetConnectionString("WmsConnection");

      
            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
               
                        foreach (var processConfig in configs.OrderBy(p=>p.Order))
                        {
                       
                            var nodes = xDoc.XPathSelectElements(processConfig.XmlIterator).ToList();

                            if (!nodes.Any()) continue; 

                           
                            var cmd = new NpgsqlCommand();
                            cmd.Connection = conn;
                            cmd.Transaction = transaction;

                            foreach (var node in nodes)
                            {
                                var colNames = new List<string>();
                                var paramNames = new List<string>();
                                cmd.Parameters.Clear();


                                bool skipRow = false;
                                // Mapeo de campos
                                foreach (var field in processConfig.Fields.OrderBy(x=>x.Id))
                                {
                                    object dbVal = null;

                                   
                                    if (field.DataType == "CURRENT_TIMESTAMP")
                                    {
                                        dbVal = DateTime.Now;
                                    }
                                    else if (string.IsNullOrEmpty(field.XmlPath))
                                    {
                                        dbVal = ConvertValue(field.DefaultValue, field.DataType);
                                    }
                                    else
                                    {
                                        /* string valStr = GetValueFromXml(node, field.XmlPath);
                                         if (string.IsNullOrEmpty(valStr) && !string.IsNullOrEmpty(field.DefaultValue))
                                         {
                                             valStr = field.DefaultValue;
                                         }
                                         dbVal = ConvertValue(valStr, field.DataType);
                                        */

                                        string valStr = null;

                                        // Variables temporales para la lógica de extracción
                                        string extractPath = field.XmlPath; // El path original
                                        string mappingRule = null;          // La regla después del pipe |

                                        // 1. DETECTAR REGLA DE MAPEO (Busca el pipe separador)
                                        if (!string.IsNullOrEmpty(extractPath) && extractPath.Contains("|"))
                                        {
                                            var parts = extractPath.Split('|', 2); // Divide en 2 partes máximo
                                            extractPath = parts[0].Trim();
                                            mappingRule = parts[1].Trim();
                                        }

                                        // 2. EXTRACCIÓN (Usando el path limpio sin la regla)
                                        if (!string.IsNullOrEmpty(extractPath))
                                        {
                                            // Soporte para plantillas {} vs Path normal
                                            if (extractPath.Contains("{") && extractPath.Contains("}"))
                                            {
                                                valStr = ResolveTemplate(node, extractPath);
                                            }
                                            else
                                            {
                                                valStr = GetValueFromXml(node, extractPath);
                                            }
                                        }

                                        // 3. TRANSFORMACIÓN (Si existía una regla)
                                        if (!string.IsNullOrEmpty(mappingRule))
                                        {
                                            valStr = ApplyValueMapping(valStr, mappingRule);
                                            // Si el resultado de la regla contiene llaves, volvemos a llamar al template resolver
                                            if (!string.IsNullOrEmpty(valStr) && valStr.Contains("{") && valStr.Contains("}"))
                                            {
                                                valStr = ResolveTemplate(node, valStr);
                                            }
                                        }

                                        // 4. VALOR POR DEFECTO (Si después de todo sigue vacío)
                                        if (string.IsNullOrEmpty(valStr) && !string.IsNullOrEmpty(field.DefaultValue))
                                        {
                                            valStr = field.DefaultValue;
                                        }

                                        // 5. CONVERSIÓN FINAL DE TIPO
                                        dbVal = ConvertValue(valStr, field.DataType);
                                    }



                                    if (field.IsKey && (dbVal == null || dbVal == DBNull.Value))
                                    {
                                        skipRow = true;
                                        break; //El campo  es llave y/o importanrte y viene nulo se salta
                                    }

                                    if (dbVal != null)
                                    {
                                        
                                        if (!string.IsNullOrEmpty(field.ReferenceTable) && !string.IsNullOrEmpty(field.ReferenceColumn))
                                        {

                                            var refParts = field.ReferenceColumn.Split('|');
                                            string mainCol = refParts[0].Trim(); // Columna llave (para validación)
                                            string extraCol = refParts.Length > 1 ? refParts[1].Trim() : null; // Columna extra (opcional)

                                            string insertCols = $"\"{mainCol}\"";
                                            string insertVals = "@valRef";

                                            if (!string.IsNullOrEmpty(extraCol))
                                            {
                                                insertCols += $", \"{extraCol}\"";
                                                insertVals += ", @valRef"; // Repetimos el valor para la segunda columna
                                            }

                                            string depSql = $@"
                                                            INSERT INTO {field.ReferenceTable} ({insertCols}, created_at, updated_at) 
                                                            VALUES ({insertVals}, @valcreated_at, @valupdated_at) 
                                                            ON CONFLICT (""{mainCol}"") DO NOTHING";

                                            using (var depCmd = new NpgsqlCommand(depSql, conn, transaction))
                                            {
                                                depCmd.Parameters.AddWithValue("@valRef", dbVal);
                                                depCmd.Parameters.AddWithValue("@valcreated_at", DateTime.Now);
                                                depCmd.Parameters.AddWithValue("@valupdated_at", DateTime.Now);
                                                Console.WriteLine(depSql);
                                                await depCmd.ExecuteNonQueryAsync();
                                            }
                                        }
                                        // -------------------------------------------------------------

                                        colNames.Add($"\"{field.DbColumn}\"");
                                        string pName = $"@p{paramNames.Count}";
                                        paramNames.Add(pName);
                                        cmd.Parameters.AddWithValue(pName, dbVal);
                                        Console.WriteLine($"CAMPOS {field.DbColumn} ** {dbVal}");
                                    }
                                }

                                // 3. Si falta alguna llave, saltamos al siguiente nodo sin hacer INSERT
                                if (skipRow) continue;

                                if (colNames.Count > 0)
                                {
                                    //cmd.CommandText = $"INSERT INTO {processConfig.TargetTable} ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";
                                    string sql = $"INSERT INTO {processConfig.TargetTable} ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";
                                    
                                    var keyFields = processConfig.Fields.Where(f => f.IsKey).ToList();

                                    if (keyFields.Any())
                                    {
                                        
                                        var keyCols = keyFields.Select(f => $"\"{f.DbColumn}\"");
                                        sql += $" ON CONFLICT ({string.Join(", ", keyCols)})";

                                        
                                        var updateFields = processConfig.Fields
                                            .Where(f => !f.IsKey && f.DbColumn != "created_at")
                                            .ToList();

                                        if (updateFields.Any())
                                        {
                                            sql += " DO UPDATE SET ";

                                            var updateParts = new List<string>();
                                            foreach (var f in updateFields)
                                            {
                                                
                                                updateParts.Add($"\"{f.DbColumn}\" = EXCLUDED.\"{f.DbColumn}\"");
                                            }

                                            sql += string.Join(", ", updateParts);
                                        }
                                        else
                                        {
                                           
                                            sql += " DO NOTHING";
                                        }
                                    }
                                    cmd.CommandText = sql;
                                    Console.WriteLine(sql);
                                    await cmd.ExecuteNonQueryAsync();
                                    totalInserted++;
                                }
                            }
                        }

                        
                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }

            return totalInserted;
        }


        private string GetValueFromXml(XElement parentNode, string path)
        {
            /* if (parentNode == null || string.IsNullOrEmpty(path)) return null;
             XElement current = parentNode;
             var parts = path.Split('/');
             foreach (var part in parts)
             {
                 if (current == null) return null;
                 current = current.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));
             }
             return current?.Value;*/
            if (parentNode == null || string.IsNullOrEmpty(path)) return null;

            XElement current = parentNode;
            var parts = path.Split('/');

            foreach (var part in parts)
            {
                if (current == null) return null;

                // Permite subir al nodo padre ---
                if (part == "..")
                {
                    current = current.Parent;
                    continue; // Saltamos al siguiente ciclo
                }
                // ------------------------------------------------

                
                current = current.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));
            }

            return current?.Value;


        }

        // Método para CONCATENAR Valores del xml
        private string ResolveTemplate(XElement node, string template)
        {
            var result = template;

            // Buscamos todas las ocurrencias de texto dentro de llaves { }
            // Usamos un loop simple para no depender de Regex si no quieres, 
            // pero funciona buscando el patrón
            int startIdx = result.IndexOf('{');
            while (startIdx >= 0)
            {
                int endIdx = result.IndexOf('}', startIdx);
                if (endIdx > startIdx)
                {
                    // Extraemos el nombre del campo, ej: "NAME1"
                    string pathKey = result.Substring(startIdx + 1, endIdx - startIdx - 1);

                    // Obtenemos el valor usando tu método existente
                    string val = GetValueFromXml(node, pathKey) ?? "";

                    // Reemplazamos "{NAME1}" por el valor real
                    // Nota: Reemplazamos solo la primera ocurrencia para mantener el loop seguro
                    string placeholder = "{" + pathKey + "}";
                    result = result.Replace(placeholder, val);

                    // Buscamos la siguiente llave
                    startIdx = result.IndexOf('{');
                }
                else
                {
                    break; // No hay cierre de llave
                }
            }
            return result;
        }



        // Método para  Poner  valor con una condicionante del xml  valor
        private string ApplyValueMapping(string rawValue, string mappingRule)
        {
            // Limpiamos el valor de entrada (nulos son vacíos)
            string currentVal = rawValue?.Trim() ?? "";

            // Separamos las reglas por punto y coma
            var rules = mappingRule.Split(';');

            foreach (var rule in rules)
            {
                var parts = rule.Split(':');
                if (parts.Length != 2) continue;

                string sourceCriteria = parts[0].Trim();
                string targetValue = parts[1].Trim();

                // 1. Chequeo de comodín global (*)
                if (sourceCriteria == "*")
                {
                    return targetValue;
                }

                // 2. Comparación exacta (Case insensitive para mayor seguridad)
                if (string.Equals(currentVal, sourceCriteria, StringComparison.OrdinalIgnoreCase))
                {
                    return targetValue;
                }
            }

            // Si ninguna regla coincide y no hubo comodín *, devolvemos el valor original
            return currentVal;
        }

        private object ConvertValue(string val, string type)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
            if (string.IsNullOrEmpty(type)) return val;
            try
            {
                return type.ToLower() switch
                {
                    "int" => int.Parse(val),
                    "decimal" => decimal.Parse(val, System.Globalization.CultureInfo.InvariantCulture),
                    "float" => float.Parse(val, System.Globalization.CultureInfo.InvariantCulture),
                    "boolean" => (val == "1" || val.ToUpper() == "TRUE" || val.ToUpper() == "X"),
                    "datetime" => DateTime.Parse(val),
                    _ => val
                };
            }
            catch
            {
                return DBNull.Value;
            }
        }

        public async Task<DataTable> GetDataForOutboundAsync(string tableName, List<int> ids)
        {
            var dataTable = new DataTable();
            var connectionString = _configuration.GetConnectionString("WmsConnection");

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // IMPORTANTE: Asumimos que la columna llave se llama "Id" o "id".
                // Si tus tablas tienen llaves diferentes (ej: order_id), 
                // tendríamos que agregar un campo 'PrimaryKeyColumn' a la tabla integrationProcess.
                // Por ahora, usaremos el estándar "Id".

                string sql = $"SELECT * FROM {tableName} WHERE \"id\" = ANY(@ids)";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    // Npgsql soporta pasar listas/arrays directamente al parámetro ANY(@ids)
                    cmd.Parameters.AddWithValue("ids", ids.ToArray());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        dataTable.Load(reader);
                    }
                }
            }

            return dataTable;
        }

        public async Task<Dictionary<int, string>> CreateOutboundXmlsAsync(string interfaceName, List<int> recordIds)
        {
            var result = new Dictionary<int, string>();

            // 1. Configuración
            var config = await _context.integrationProcesses
                .Include(p => p.Fields)
                .FirstOrDefaultAsync(p => p.InterfaceName == interfaceName);

            if (config == null || string.IsNullOrEmpty(config.XmlTemplate))
                throw new Exception($"Configuración incompleta para '{interfaceName}'.");

            // 2. Obtener Headers (Tabla Principal)
            var dtHeader = await GetDataForOutboundAsync(config.TargetTable, recordIds);

            // 3. Obtener Lines (Tabla Detalle) - Si está configurada
            DataTable dtLines = null;
            string fkColumnName = "";

            if (!string.IsNullOrEmpty(config.DetailTable))
            {
                // Asumimos convención: La FK en la tabla hija se llama igual que la tabla padre + "_id" 
                // O mejor: Usamos una convención simple "incoming_goods_id" si la tabla es "incoming_goods"
                // Para hacerlo genérico rápido, asumiremos que la columna FK se llama "incoming_goods_id" 
                // (basado en el nombre de la tabla target: erp.incoming_goods -> incoming_goods_id)

              fkColumnName = config.TargetTable.Split('.').Last() + "_id";
                dtLines = await GetDataForLinesAsync(config.DetailTable, fkColumnName, recordIds);
            }

            // 4. Procesar
            foreach (DataRow headerRow in dtHeader.Rows)
            {
                int recordId = Convert.ToInt32(headerRow["Id"]);
                string currentXml = config.XmlTemplate;

                // Filtrar las líneas de este registro específico
                //DataRow[] currentLines = dtLines?.Select($"{dtLines.Columns[1].ColumnName} = {recordId}") ?? new DataRow[0];
                DataRow[] currentLines = new DataRow[0];

                if (dtLines != null && !string.IsNullOrEmpty(fkColumnName))
                {
                    // El filtro busca filas donde la columna FK coincida con el ID del padre
                    currentLines = dtLines.Select($"{fkColumnName} = {recordId}");
                }
                // Contexto para Header: Usamos el Header Row, pero si falta dato, usamos la Primera Línea
                // Esto soluciona tu problema de que WERKS está en las líneas.
                DataRow contextRowForHeader = headerRow;
                DataRow fallbackRow = currentLines.Length > 0 ? currentLines[0] : null;

                // A. REEMPLAZO DE VARIABLES DEL TEMPLATE (Header/Metadata)
                var templateFields = config.Fields.Where(f => f.XmlPath.StartsWith("{"));
                foreach (var field in templateFields)
                {
                    string val = GetValueFromRows(field.DbColumn, contextRowForHeader, fallbackRow);
                    currentXml = currentXml.Replace(field.XmlPath, val);
                }

                // B. PARSEO DEL DOM
                XDocument xDoc = XDocument.Parse(currentXml);

                // C. LLENADO DE ENCABEZADO (BodyNodeName)
                if (!string.IsNullOrEmpty(config.BodyNodeName))
                {
                    var headerNode = xDoc.Descendants().FirstOrDefault(x => x.Name.LocalName == config.BodyNodeName);
                    if (headerNode != null)
                    {
                        // Campos que NO son template y NO tienen ReferenceTable (asumimos que ReferenceTable marca los detalles)
                        var headerBodyFields = config.Fields.Where(f => !f.XmlPath.StartsWith("{") && string.IsNullOrEmpty(f.ReferenceTable));

                        foreach (var field in headerBodyFields)
                        {
                            // AQUÍ ESTÁ EL TRUCO: Buscamos en Header, si no está, buscamos en la primera línea
                            string val = GetValueFromRows(field.DbColumn, contextRowForHeader, fallbackRow);
                            AddChildElement(headerNode, field.XmlPath, val);
                        }
                    }
                }

                // D. LLENADO DE DETALLE (DetailNodeName)
                if (!string.IsNullOrEmpty(config.DetailNodeName) && currentLines.Any())
                {
                    // Buscamos el nodo PADRE donde se insertarán los items del detalle.
                    // En SAP, suele ser que DT_MovInvDetalleSAP es una lista de items, 
                    // o que dentro de DT_MovInvDetalleSAP van muchos nodos 'Item'.
                    // Asumiremos que el nodo en el XML Template (ej: <DT_MovInvDetalleSAP />) es el CONTENEDOR.

                    // Campos marcados para detalle (usaremos ReferenceTable = 'Detail' o el nombre de la tabla detalle)
                    var lineFields = config.Fields.Where(f => !string.IsNullOrEmpty(f.ReferenceTable));

                    // Buscamos el contenedor en el XML (ej: DT_MovInvDetalleSAP)
                    // OJO: En tu XML ejemplo, DT_MovInvDetalleSAP parece ser el nodo repetitivo? 
                    // Usualmente en SAP: <Padre><Lineas><Item>...</Item><Item>...</Item></Lineas></Padre>
                    // Si tu XML espera: <DT_MovInvDetalleSAP><Campo>...</Campo></DT_MovInvDetalleSAP> repetido N veces,
                    // entonces necesitamos encontrar al PADRE del nodo detalle.

                    // Para simplificar: Buscamos el nodo raíz o el padre lógico y añadimos nodos hermanos.
                    // Pero según tu estructura, DT_MovInvDetalleSAP parece ser hermano de DT_MovInvEncabezadoSAP.

                    // ESTRATEGIA: Buscamos el nodo <DT_MovInvDetalleSAP> en el template.
                    // Si existe, lo usaremos como "Plantilla de Item" o como "Contenedor".
                    // Si el XML requiere repetir el nodo <DT_MovInvDetalleSAP> por cada línea:
                    var containerNode = xDoc.Root; // Insertamos directo en la raíz (MT_MovInventarioSAP)

                    foreach (var lineRow in currentLines)
                    {
                        // Creamos un NUEVO nodo por cada línea
                        XElement lineNode = new XElement(XName.Get(config.DetailNodeName, xDoc.Root.Name.NamespaceName));

                        foreach (var field in lineFields)
                        {
                            string val = GetValueFromRows(field.DbColumn, lineRow, null); // Solo buscamos en la línea
                            AddChildElement(lineNode, field.XmlPath, val);
                        }

                        // Lo agregamos al documento (al final, después del encabezado)
                        xDoc.Root.Add(lineNode);
                    }
                }

                result.Add(recordId, xDoc.ToString());
            }

            return result;
        }

        // Helper para buscar valor en Header O en Línea
        private string GetValueFromRows(string colName, DataRow mainRow, DataRow? fallbackRow)
        {
            if (colName.StartsWith("GENERATE_"))
            {
                if (colName == "GENERATE_GUID") return Guid.NewGuid().ToString("N");
                if (colName == "GENERATE_QUICK_ID") return $"924-{DateTime.Now:yyyyMMddHHmmssfff}";
                return "";
            }

            // 1. Buscar en Tabla Principal
            if (mainRow.Table.Columns.Contains(colName))
            {
                var val = mainRow[colName];
                if (val != DBNull.Value) return ConvertToString(val);
            }

            // 2. Si no está o es nulo, buscar en Tabla Secundaria (Fallback)
            if (fallbackRow != null && fallbackRow.Table.Columns.Contains(colName))
            {
                var val = fallbackRow[colName];
                if (val != DBNull.Value) return ConvertToString(val);
            }

            return ""; // O valor por defecto del campo si lo pasáramos
        }

        private string ConvertToString(object val)
        {
            if (val is DateTime d) return d.ToString("yyyy-MM-dd"); // Formato SAP estándar
            return val.ToString();
        }

        // Nuevo Helper para traer las líneas
        private async Task<DataTable> GetDataForLinesAsync(string tableName, string fkColName, List<int> parentIds)
        {
            var dt = new DataTable();
            // Nota: fkColName debe ser seguro (sin inyección). Como viene de config interna, se asume seguro.
            string sql = $"SELECT * FROM {tableName} WHERE \"{fkColName}\" = ANY(@ids)";

            using (var conn = new NpgsqlConnection(_configuration.GetConnectionString("WmsConnection")))
            {
                await conn.OpenAsync();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("ids", parentIds.ToArray());
                    using (var reader = await cmd.ExecuteReaderAsync()) dt.Load(reader);
                }
            }
            return dt;
        }

        // HELPER: Crea nodos hijos recursivamente (Ej: "Header/Date")
        private void AddChildElement(XElement parent, string path, string value)
        {
            var parts = path.Split('/');
            XElement current = parent;

            foreach (var part in parts)
            {
                // Buscamos si ya existe el nodo hijo (ignorando namespace para facilitar)
                XElement next = current.Elements()
                    .FirstOrDefault(e => e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (next == null)
                {
                    // Creamos el nodo. 
                    // IMPORTANTE: Al crearlo sin namespace, heredará el del padre o quedará vacío.
                    // Si SAP es estricto con namespaces en hijos, tal vez necesitemos tomar el namespace del padre.
                    // Por ahora, lo creamos simple:
                    next = new XElement(current.Name.Namespace + part);
                    current.Add(next);
                }
                current = next;
            }
            // Asignar valor al nodo hoja
            current.Value = value;
        }
    }
}