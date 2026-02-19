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
                    Order=1,
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
                    Order=2,
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
                    Order=3,
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
                },new integrationProcess
                {
                    ProcessName="SAP_MATERIAL_FAMYLIN",
                    InterfaceName="MMI019",
                    TargetTable="erp.item_subgroup",
                    XmlIterator="//*[local-name()='DT_MaterialesDetalleSAP']",
                    Order=4,
                    Fields = new List<integrationField>
                    {
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="code",DataType="string",IsKey=true},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT6",DbColumn="name",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="line_code",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT5",DbColumn="line_name",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="brand_code",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT4",DbColumn="brand_name",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="super_brand_code",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT3",DbColumn="super_brand_name",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="family_code",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT2",DbColumn="family_name",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="PRDHA",DbColumn="super_family_code",DataType="string"},
                        new integrationField {ProcessName="SAP_MATERIAL_FAMYLIN",XmlPath="VTEXT1",DbColumn="super_family_name",DataType="string"},


                    }
                },
                new integrationProcess
                {
                    ProcessName="SDI003_PARTNER_HEAD",
                    InterfaceName="SDI003",
                    TargetTable="erp.partner",
                    XmlIterator="//*[local-name()='DT_ClientesDetalleSAP' and *[local-name()='KTOKD']='0002']",
                    Order=1,
                    Fields = new List<integrationField>
                    {
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",XmlPath="KUNNR",DbColumn="code",DataType="string",IsKey=true},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",XmlPath="STCD1",DbColumn="rfc",DataType="string"},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",DbColumn="type",DataType="string",DefaultValue="Cliente"},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",DbColumn="created_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",XmlPath="{NAME1} {NAME4}",DbColumn="name",DataType="string"},
                        new integrationField {ProcessName="SDI003_PARTNER_HEAD",DbColumn="society_type",DataType="string",DefaultValue="cliente"}
                    }
                },
                new integrationProcess
                {
                    ProcessName="SDI003_ADDR_MAIN",
                    InterfaceName="SDI003",
                    TargetTable="erp.partner_addresses",
                    XmlIterator="//*[local-name()='DT_ClientesDetalleSAP' and *[local-name()='KTOKD']='0002']",
                    Order=2,
                    Fields = new List<integrationField>
                    {
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="SUC_CLAVE | :{KUNNR}; *:{SUC_CLAVE}",DbColumn="code",DataType="string",IsKey=true},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="KUNNR",DbColumn="partner_code",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="HOUSE_NUM1",DbColumn="exterior_number",DataType="int"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="../DT_MasterData/CENTRO",DbColumn="cedis_code",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="LOEVM | X:false; *:true",DbColumn="active",DataType="boolean"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",DbColumn="created_at",DataType="CURRENT_TIMESTAMP"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="POST_CODE1",DbColumn="postal_code",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="BEZEI",DbColumn="state",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="CITY2",DbColumn="neighborhood",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="STREET",DbColumn="street",DataType="string"},
                         new integrationField {ProcessName="SDI003_ADDR_MAIN",XmlPath="{NAME1} {NAME4}",DbColumn="address_name",DataType="string"}



                    }

                },
                new integrationProcess
                {
                    ProcessName="SDI003_ADDR_BRANCH",
                    InterfaceName="SDI003",
                    TargetTable="erp.partner_addresses",
                    XmlIterator="//*[local-name()='DT_ClientesDetalleSAP' and *[local-name()='KTOKD']='0018']",
                    Order=3,
                    Fields = new List<integrationField>
                    {
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="SUC_CLAVE",DbColumn="code",DataType="string",IsKey=true},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="KUNNR",DbColumn="partner_code",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="HOUSE_NUM1",DbColumn="exterior_number",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",DbColumn="updated_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",DbColumn="created_at",DataType="CURRENT_TIMESTAMP"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="POST_CODE1",DbColumn="postal_code",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="BEZEI",DbColumn="state",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="CITY2",DbColumn="neighborhood",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="STREET",DbColumn="street",DataType="string"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="{NAME1} {NAME4}",DbColumn="address_name",DataType="string" },
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="LOEVM | X:false; *:true",DbColumn="active",DataType="boolean"},
                        new integrationField {ProcessName="SDI003_ADDR_BRANCH",XmlPath="../DT_MasterData/CENTRO",DbColumn="cedis_code",DataType="string"},
                    }
                },
                new integrationProcess
                {
                    ProcessName="MMI014_OUTBOUND",
                    InterfaceName="MMI014",
                    TargetTable="erp.goods_adjustment",
                    XmlIterator="N/A",
                    Order=1,
                    BodyNodeName="DT_MovInvEncabezadoSAP",
                    XmlTemplate="<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n    <n0:MT_MovInventarioSAP xmlns:n0=\"http://grupolala.com/sicavcedis/ventas/MM-I-014\">\r\n        <DT_ControlData>\r\n            <QUICK_ID>{QUICK_ID}</QUICK_ID>\r\n            <INTERFACE_NAME>{INTERFACE_NAME}</INTERFACE_NAME>\r\n            <LOG_ID>{LOG_ID}</LOG_ID>\r\n            <SOURCE_SYSTEM>ED0</SOURCE_SYSTEM>\r\n        </DT_ControlData>\r\n        <DT_MasterData>\r\n            <CENTRO>{CENTRO}</CENTRO>\r\n        </DT_MasterData>\r\n        <DT_MovInvEncabezadoSAP>\r\n             </DT_MovInvEncabezadoSAP>\r\n    </n0:MT_MovInventarioSAP>",
                    DetailNodeName="DT_MovInvDetalleSAP",
                    DetailTable="erp.goods_adjustment_line",
                    Fields = new List<integrationField>
                    {
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="{QUICK_ID}",DbColumn="GENERATE_QUICK_ID",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="{INTERFACE_NAME}",DbColumn="module",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="{LOG_ID}",DbColumn="log_id",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="{ORIGIN}",DbColumn="origin",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="{CENTRO}",DbColumn="warehouse_code",DataType="string"},                        
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="BLDAT",DbColumn="created_at",DataType="datetime"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="WERKS",DbColumn="warehouse_code",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="CONCEPTO",DbColumn="",DataType="string",DefaultValue="IE"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="REFERENCIA",DbColumn="erp_reference",DataType="string"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="MATNR",DbColumn="item_code",DataType="string",ReferenceTable="Detail"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="BISDAT",DbColumn="updated_at",DataType="datetime",ReferenceTable="Detail"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="CHARG",DbColumn="batch",DataType="string",ReferenceTable="Detail"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="ERFMG",DbColumn="quantity",DataType="decimal",ReferenceTable="Detail"},
                        new integrationField {ProcessName="MMI014_OUTBOUND",XmlPath="MENGE",DbColumn="quantity",DataType="decimal",ReferenceTable="Detail"}
                    }
                }
            };
            return procesos;

        }
    }
}