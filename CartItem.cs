using System.ComponentModel.DataAnnotations;

namespace ProductApi.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty; 
        [Required]
        public int ProductId { get; set; }
        [Required]
        [Range(1, 9999)]
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }
}
