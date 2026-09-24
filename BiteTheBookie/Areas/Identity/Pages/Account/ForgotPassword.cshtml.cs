using System.ComponentModel.DataAnnotations;
using BiteTheBookie.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BiteTheBookie.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user != null && await _userManager.IsEmailConfirmedAsync(user))
            {
                // A password reset token can be generated here and emailed once an
                // IEmailSender is configured. Do not reveal that the user does not
                // exist or is not confirmed.
                await _userManager.GeneratePasswordResetTokenAsync(user);
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
