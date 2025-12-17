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
        [MaxLength(50)]
        public string InterfaceName { get; set; } // Ej: "MMI019" (Puede repetirse en varios procesos)

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
        public string ProcessName { get; set; } 

        [MaxLength(200)]
        public string? XmlPath { get; set; }    

        [Required]
        [MaxLength(100)]
        public string DbColumn { get; set; }  

        [MaxLength(50)]
        public string DataType { get; set; }   

        public string? DefaultValue { get; set; } 

        [MaxLength(100)]
        public string? ReferenceTable { get; set; }  

        [MaxLength(100)]
        public string? ReferenceColumn { get; set; } 
        public bool IsKey { get; set; } = false; 
       
        [ForeignKey("ProcessName")]
        public IntegrationProcess Process { get; set; }
    }
}
