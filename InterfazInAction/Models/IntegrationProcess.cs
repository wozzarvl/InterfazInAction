using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfazInAction.Models
{
    public class IntegrationProcess
    {
        [Key]
        [MaxLength(50)]
        public string ProcessName { get; set; } // Ej: "SAP_MATERIAL_IMPORT"

        [Required]
        [MaxLength(100)]
        public string TargetTable { get; set; } // Ej: "erp.item"

        [Required]
        [MaxLength(200)]
        public string XmlIterator { get; set; } // XPath para iterar (Ej: "//*[local-name()='DT_MaterialesDetalleSAP']")

        // Relación con los campos
        public List<IntegrationField> Fields { get; set; }
    }
    // TABLA DETALLE: Define el mapeo campo a campo
    public class IntegrationField
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProcessName { get; set; } // Foreign Key

        [MaxLength(200)]
        public string XmlPath { get; set; }    // Tag en XML (Ej: "MATNR"). Puede ser NULL para valores fijos.

        [Required]
        [MaxLength(100)]
        public string DbColumn { get; set; }   // Columna en Postgres (Ej: "code")

        [MaxLength(50)]
        public string DataType { get; set; }   // "string", "int", "decimal", "CURRENT_TIMESTAMP"

        public string DefaultValue { get; set; } // Valor usado si XmlPath es null o no viene en el XML

        [MaxLength(100)]
        public string ReferenceTable { get; set; }  // Ej: "erp.item_group"

        [MaxLength(100)]
        public string ReferenceColumn { get; set; } // Ej: "name"

        // Navegación (opcional, pero recomendada para EF Core)
        [ForeignKey("ProcessName")]
        public IntegrationProcess Process { get; set; }
    }
}
