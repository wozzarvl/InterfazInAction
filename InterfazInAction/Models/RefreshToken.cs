namespace InterfazInAction.Models
{
    internal class RefreshToken
    {
        public string Token { get; set; } // El string aleatorio
        public string Usuario { get; set; } // A quién pertenece
        public DateTime Expires { get; set; } // Cuándo caduca (usaremos tus 7 días)
        public DateTime Created { get; set; } = DateTime.Now; // Cuándo se creó
        public DateTime? Revoked { get; set; } // Si se invalidó manualmente
        public bool IsActive => Revoked == null && DateTime.Now < Expires; // Helper útil
    }
}