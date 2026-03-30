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
        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null) { Message = "User not found."; return Page(); }

        await _userManager.AddToRoleAsync(user, Role);
        Message = $"{Email} added to {Role}.";
        return Page();
    }
}