using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BiteTheBookie.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersModel(UserManager<ApplicationUser> userManager)
        => _userManager = userManager;

    public IReadOnlyList<UserRow> Users { get; private set; } = new List<UserRow>();

    public int TotalUsers { get; private set; }
    public int PaidUsers { get; private set; }
    public int CancelledUsers { get; private set; }

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var rows = new List<UserRow>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            rows.Add(new UserRow(user, roles));
        }

        Users = rows;
        TotalUsers = rows.Count;
        PaidUsers = rows.Count(r => r.IsPro);
        CancelledUsers = rows.Count(r => r.User.SubscriptionCancelled);
    }

    public class UserRow
    {
        public UserRow(ApplicationUser user, IEnumerable<string> roles)
        {
            User = user;
            Roles = roles.ToList();
        }

        public ApplicationUser User { get; }
        public IReadOnlyList<string> Roles { get; }

        public string FullName =>
            string.Join(" ", new[] { User.FirstName, User.LastName }
                .Where(n => !string.IsNullOrWhiteSpace(n)));

        public bool IsPro => User.IsPro;
    }
}
