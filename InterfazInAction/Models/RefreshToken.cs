namespace InterfazInAction.Models
{
    internal class RefreshToken
    {
        public string Token { get; set; } 
        public string Usuario { get; set; } 
        public DateTime Expires { get; set; } 
        public DateTime Created { get; set; } = DateTime.Now; 
        public DateTime? Revoked { get; set; } 
        public bool IsActive => Revoked == null && DateTime.Now < Expires; 
    }
}