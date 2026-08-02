using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Validates that a <see cref="DateTime"/> value is at least the specified number of
    /// years in the past (i.e. the person is at least <see cref="_minimumAge"/> years old).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MinimumAgeAttribute : ValidationAttribute
    {
        private readonly int _minimumAge;

        public MinimumAgeAttribute(int minimumAge)
        {
            _minimumAge = minimumAge;
            ErrorMessage = $"You must be at least {minimumAge} years old to register.";
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not DateTime dob)
            {
                // Let [Required] handle missing values.
                return ValidationResult.Success;
            }

            var today = DateTime.Today;

            if (dob.Date > today)
            {
                return new ValidationResult("Date of birth cannot be in the future.");
            }

            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age))
            {
                age--; // birthday hasn't occurred yet this year
            }

            return age >= _minimumAge
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage);
        }
    }
}
