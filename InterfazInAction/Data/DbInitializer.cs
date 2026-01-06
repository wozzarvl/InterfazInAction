using InterfazInAction.Models;
using Microsoft.EntityFrameworkCore;

namespace InterfazInAction.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
         
            try
            {
                context.Database.Migrate();

                if (context.integrationProcesses.Any())
                {
                    return; // La base de datos ya ha sido sembrada
                }

                Console.WriteLine("Insertar informacion de base datos por default");
                context.integrationProcesses.AddRange(IntegrationProcessDefaults());
                context.SaveChanges();

            }
            catch (Exception ex)
            {
                
                Console.WriteLine($"--> Error al aplicar migraciones: {ex.Message}");
                throw; 
            }

          
            if (context.users.Any())
            {
                return;   // La DB ya tiene datos
            }

            Console.WriteLine("--> Sembrando base de datos con usuario Admin...");

            // Crear el usuario Administrador por defecto
            var adminUser = new user
            {
                UserName = "admin",
                // Importante: Usamos BCrypt para hashear la contraseña "l4l4In4ct10n"
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("l4l4In4ct10n"),
                Role = "Administrador",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.users.Add(adminUser);


            context.SaveChanges();
            Console.WriteLine("--> Base de datos sembrada exitosamente.");
        }

        private static integrationProcess[] IntegrationProcessDefaults()
        {
            var procesos = new integrationProcess[]
            {
                new integrationProcess 
                { 
                    ProcessName= "SAP_MATERIAL_ITEM",
                    InterfaceName="MMI019",
                    TargetTable="erp.item",
                    XmlIterator= "//*[local-name()='DT_MaterialesDetalleSAP']",
                    Fields =new List<integrationField> 
                    {
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",XmlPath="MATNR", DbColumn="code",DataType="string",IsKey=true},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",XmlPath="MAKTX",DbColumn="description",DataType="string"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",XmlPath="NTGEW",DbColumn="weight",DataType="decimal"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",DbColumn="management_type",DataType="string",DefaultValue="Standard"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",DbColumn="created_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",XmlPath="VTEXT1",DbColumn="item_group_name",DataType="string",ReferenceTable="erp.item_group",ReferenceColumn="name"},
                        new integrationField { ProcessName="SAP_MATERIAL_ITEM",XmlPath="MEINS",DbColumn="measure_unit_name",DataType="string",ReferenceTable="erp.measure_unit",ReferenceColumn="name",
                        }

                    }
                },
                new integrationProcess
                {   
                    ProcessName= "SAP_MATERIAL_SKU",
                    InterfaceName="MMI019",
                    TargetTable="erp.sku",
                    XmlIterator= "//*[local-name()='DT_MaterialesDetalleSAP']",
                    Fields  = new List<integrationField>
                    {
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="EAN11",DbColumn="barcode",DataType="string",IsKey=true},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="MATNR",DbColumn="item_code",DataType="string"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="MEINS",DbColumn="type",DataType="string"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="MEINS",DbColumn="measure_unit_name",DataType="string",ReferenceTable="erp.measure_unit",ReferenceColumn="name"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="UMREZunidadPorCa",DbColumn="equivalence",DataType="decimal"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",DbColumn="created_at",DataType="CURRENT_TIMESTAMP" },
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP" },

                    }
                },
                new integrationProcess
                {
                    ProcessName= "SAP_MATERIAL_UOM",
                    InterfaceName="MMI019",
                    TargetTable="erp.sku",
                    XmlIterator= "//*[local-name()='DT_UM']",
                    Fields  = new List<integrationField>
                    {
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="EAN11",DbColumn="barcode",DataType="string",IsKey=true},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="../MATNR",DbColumn="item_code",DataType="string"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="MEINH",DbColumn="type",DataType="string"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="MEINH",DbColumn="measure_unit_name",DataType="string",ReferenceTable="erp.measure_unit",ReferenceColumn="name"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",XmlPath="UMREZ",DbColumn="equivalence",DataType="decimal"},
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",DbColumn="created_at",DataType="CURRENT_TIMESTAMP" },
                        new integrationField { ProcessName="SAP_MATERIAL_SKU",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP" },

                    }
                }
            };
            return procesos;

        }
    }
}