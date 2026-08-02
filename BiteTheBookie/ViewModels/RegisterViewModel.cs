using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        [MinimumAge(18)]
        public DateTime? DateOfBirth { get; set; }

        [Required]
        [Display(Name = "Street Address")]
        [StringLength(200)]
        public string StreetAddress { get; set; } = string.Empty;

        [Required]
        [Display(Name = "City")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [Display(Name = "State")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Use the two-letter state code.")]
        public string State { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Zip Code")]
        [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Enter a valid ZIP code (12345 or 12345-6789).")]
        public string ZipCode { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Phone Number")]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// free, premium, or vip
        /// </summary>
        public string? SelectedPlan { get; set; } = "free";
    }
}