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
            
            var configs = await _context.IntegrationProcesses
                .Include(p => p.Fields)
                .Where(p => p.InterfaceName == interfaceName)
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
               
                        foreach (var processConfig in configs)
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
                                        string valStr = GetValueFromXml(node, field.XmlPath);
                                        if (string.IsNullOrEmpty(valStr) && !string.IsNullOrEmpty(field.DefaultValue))
                                        {
                                            valStr = field.DefaultValue;
                                        }
                                        dbVal = ConvertValue(valStr, field.DataType);
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
                                    }
                                }

                                
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
            if (parentNode == null || string.IsNullOrEmpty(path)) return null;
            XElement current = parentNode;
            var parts = path.Split('/');
            foreach (var part in parts)
            {
                if (current == null) return null;
                current = current.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(part, StringComparison.OrdinalIgnoreCase));
            }
            return current?.Value;
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