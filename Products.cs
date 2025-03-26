using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ProductApi.Models
{
    public class Product
    {
        public int Id {get; set;}
        public string? Name {get; set;}
        public decimal Price { get; set;}
        public int Stock {get; set;}

        public string? ImageUrl { get; set; }
        public string? ImageFileName { get; set; }
      }

      public class UserProfile
    {
        public int Id { get; set; }
        [Required]
        public string? GitHubId { get; set; }
        [Required]
        public string? Username { get; set; }
        // Editable by the user
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
        [EmailAddress]
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        // Role: "customer", "administrator", or "vendor"
        [Required]
        public string? Role { get; set; }
    }
  }