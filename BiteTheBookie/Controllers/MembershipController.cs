using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class MembershipController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<MembershipController> _logger;
        private readonly IMembershipService _membershipService;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<MembershipController> logger,
            IMembershipService membershipService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _membershipService = membershipService;
        }

        /// <summary>
        /// Join / Pricing page - shows membership tiers
        /// </summary>
        public IActionResult Join()
        {
            return View();
        }

        /// <summary>
        /// Registration form
        /// </summary>
        [HttpGet]
        public IActionResult Register(string? plan)
        {
            var model = new RegisterViewModel
            {
                SelectedPlan = plan ?? "free"
            };
            return View(model);
        }

        /// <summary>
        /// Handle registration
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                DateOfBirth = model.DateOfBirth,
                StreetAddress = model.StreetAddress,
                City = model.City,
                State = model.State,
                ZipCode = model.ZipCode,
                PhoneNumber = model.PhoneNumber,
                SubscriptionTier = model.SelectedPlan?.ToLower() switch
                {
                    "pro" => SubscriptionTier.Pro,
                    "allaccess" => SubscriptionTier.AllAccess,
                    _ => SubscriptionTier.Free
                },
                SubscriptionExpiry = model.SelectedPlan?.ToLower() switch
                {
                    "pro" => DateTime.UtcNow.AddMonths(1),
                    "allaccess" => DateTime.UtcNow.AddMonths(1),
                    _ => null
                },
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} created a new account with plan {Plan}.", model.Email, model.SelectedPlan);

                // Add role
                var roleName = user.SubscriptionTier switch
                {
                    SubscriptionTier.AllAccess => "AllAccess",
                    SubscriptionTier.Pro => "Pro",
                    _ => "Free"
                };
                await _userManager.AddToRoleAsync(user, roleName);

                // Add subscription claim
                await _userManager.AddClaimAsync(user, 
                    new System.Security.Claims.Claim("SubscriptionTier", user.SubscriptionTier.ToString()));

                // Keep the in-memory membership state in sync with the persisted tier.
                _membershipService.UpdateMembershipLevel(user.Id, user.SubscriptionTier);

                // If paid plan, redirect to payment (placeholder)
                if (user.SubscriptionTier != SubscriptionTier.Free)
                {
                    // TODO: Integrate Stripe/PayPal here
                    // For now, sign in and redirect to a confirmation page
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("PaymentConfirmation", new { plan = model.SelectedPlan });
                }

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        /// <summary>
        /// Payment confirmation placeholder
        /// </summary>
        [Authorize]
        public IActionResult PaymentConfirmation(string plan)
        {
            ViewBag.Plan = plan;
            return View();
        }

        /// <summary>
        /// Account dashboard showing subscription status
        /// </summary>
        [Authorize]
        public async Task<IActionResult> MyAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Join");

            var model = new MyAccountViewModel
            {
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                SubscriptionTier = user.SubscriptionTier,
                SubscriptionExpiry = user.SubscriptionExpiry,
                IsPremium = user.IsPremium,
                MemberSince = user.CreatedAt
            };

            return View(model);
        }

        /// <summary>
        /// Upgrade subscription
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upgrade(string plan)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Join");

            var newTier = plan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

            // TODO: Process payment via Stripe/PayPal before upgrading

            user.SubscriptionTier = newTier;
            user.SubscriptionExpiry = DateTime.UtcNow.AddMonths(1);

            await _userManager.UpdateAsync(user);

            // Update claims
            var existingClaims = await _userManager.GetClaimsAsync(user);
            var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, tierClaim);
            }
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim("SubscriptionTier", newTier.ToString()));

            // Keep the in-memory membership state in sync with the persisted tier.
            _membershipService.UpdateMembershipLevel(user.Id, newTier);

            // Refresh sign-in to update claims
            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("MyAccount");
        }

        [HttpGet]
        public IActionResult Index(string userId)
        {
            var tier = _membershipService.GetUserMembership(userId);
            var features = MembershipFeatures.Description(tier);

            return View("MembershipDashboard", new
            {
                MembershipLevel = tier,
                Features = features
            });
        }

        [HttpPost]
        public IActionResult UpgradeLevel(string userId, SubscriptionTier newLevel)
        {
            if (_membershipService.UpdateMembershipLevel(userId, newLevel))
            {
                return RedirectToAction("Index", new { userId });
            }

            return BadRequest("Upgrade failed.");
        }
    }
}