using BiteTheBookie.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

[Authorize(Roles = "Admin")]
public class ManageRolesModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ManageRolesModel(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Role { get; set; } = string.Empty;
    public string? Message { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Role))
        {
            Message = "Please provide both an email and a role.";
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null) { Message = "User not found."; return Page(); }

        if (await _userManager.IsInRoleAsync(user, Role))
        {
            Message = $"{Email} is already in the {Role} role.";
            return Page();
        }

        var result = await _userManager.AddToRoleAsync(user, Role);
        if (!result.Succeeded)
        {
            Message = "Could not assign role: " + string.Join(" ", result.Errors.Select(e => e.Description));
            return Page();
        }

        Message = $"{Email} added to {Role}.";
        return Page();
    }
}