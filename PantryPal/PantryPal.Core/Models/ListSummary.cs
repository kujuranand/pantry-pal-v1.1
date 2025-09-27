namespace PantryPal.Core.Models
{
    public sealed class ListSummary
    {
        public int Id { get; set; }                  
        public string Name { get; set; } = "";       
        public DateTime CreatedUtc { get; set; }     
        public DateTime? PurchasedUtc { get; set; }  
        public int ItemCount { get; set; }           
        public decimal TotalCost { get; set; }       
    }
}
