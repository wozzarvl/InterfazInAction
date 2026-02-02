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
                                foreach (var field in processConfig.Fields)
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
                                            
                                            string depSql = $@"
                                                INSERT INTO {field.ReferenceTable} (""{field.ReferenceColumn}"",created_at,updated_at) 
                                                VALUES (@valRef,@valcreated_at,@valupdated_at) 
                                                ON CONFLICT (""{field.ReferenceColumn}"") DO NOTHING";

                                            using (var depCmd = new NpgsqlCommand(depSql, conn, transaction))
                                            {
                                                depCmd.Parameters.AddWithValue("@valRef", dbVal);
                                                depCmd.Parameters.AddWithValue("@valcreated_at", DateTime.Now);
                                                depCmd.Parameters.AddWithValue("@valupdated_at", DateTime.Now);
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
    }
}