using System.ComponentModel.DataAnnotations;

namespace ProductApi.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        [Required]
        public int OrderId { get; set; }
        [Required]
        public int ProductId { get; set; }
        [Required]
        [Range(1, 9999)]
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
