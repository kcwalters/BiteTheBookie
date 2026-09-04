using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Implementations;
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
        private readonly PayPalService _payPalService;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            PayPalService payPalService,
            ILogger<MembershipController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _payPalService = payPalService;
            _logger = logger;
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
                // Everyone starts as Free. Paid tiers are granted only after PayPal confirms payment.
                SubscriptionTier = SubscriptionTier.Free,
                SubscriptionExpiry = null,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} created a new account with plan {Plan}.", model.Email, model.SelectedPlan);

                // Everyone is created as Free; paid access is granted only after payment is confirmed.
                await _userManager.AddToRoleAsync(user, "Free");

                // Add subscription claim
                await _userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("SubscriptionTier", SubscriptionTier.Free.ToString()));

                await _signInManager.SignInAsync(user, isPersistent: false);

                var selectedPlan = model.SelectedPlan?.ToLowerInvariant();
                var isPaidPlan = selectedPlan == "pro" || selectedPlan == "allaccess";

                // If a paid plan was chosen, send the user to the on-site payment page
                // (PayPal hosted card fields) so they can pay by card without a PayPal account.
                if (isPaidPlan)
                {
                    if (!_payPalService.IsConfigured)
                    {
                        _logger.LogWarning("PayPal is not configured; cannot start subscription for plan {Plan}.", selectedPlan);
                        ModelState.AddModelError(string.Empty, "Online payments are not currently available. Please try again later.");
                        return View(model);
                    }

                    return RedirectToAction("Payment", new { plan = selectedPlan });
                }

                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        /// <summary>
        /// On-site payment page: renders PayPal hosted card fields + button so the user can
        /// subscribe with a debit/credit card without leaving the site or creating a PayPal account.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Payment(string plan)
        {
            var selectedPlan = plan?.ToLowerInvariant();
            var isPaidPlan = selectedPlan == "pro" || selectedPlan == "allaccess";
            if (!isPaidPlan)
            {
                return RedirectToAction("Join");
            }

            if (!_payPalService.IsConfigured)
            {
                _logger.LogWarning("PayPal is not configured; cannot render payment page for plan {Plan}.", selectedPlan);
                TempData["PaymentError"] = "Online payments are not currently available. Please try again later.";
                return RedirectToAction("Join");
            }

            var planId = _payPalService.GetPlanId(selectedPlan);
            if (string.IsNullOrWhiteSpace(planId))
            {
                _logger.LogWarning("No PayPal plan id configured for plan {Plan}.", selectedPlan);
                TempData["PaymentError"] = "This plan is not available right now. Please try again later.";
                return RedirectToAction("Join");
            }

            string clientToken;
            try
            {
                clientToken = await _payPalService.GenerateClientTokenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate PayPal client token for plan {Plan}.", selectedPlan);
                TempData["PaymentError"] = "There was an issue starting checkout. Please try again later.";
                return RedirectToAction("Join");
            }

            ViewBag.Plan = selectedPlan;
            ViewBag.PlanId = planId;
            ViewBag.ClientId = _payPalService.ClientId;
            ViewBag.ClientToken = clientToken;
            ViewBag.PlanName = selectedPlan == "allaccess" ? "All Access" : "Pro";
            ViewBag.PlanPrice = selectedPlan == "allaccess" ? "$19.99" : "$9.99";

            return View();
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
                IsPro = user.IsPro,
                MemberSince = user.CreatedAt
            };

            return View(model);
        }

        /// <summary>
        /// Validate PayPal subscription and apply selected tier
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubscription(string subscriptionId, string plan)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(subscriptionId)) return BadRequest("Missing subscription ID.");

            try
            {
                var isValid = await _payPalService.VerifySubscription(subscriptionId);
                if (!isValid) return StatusCode(403, "Failed to verify subscription.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify PayPal subscription with ID {SubscriptionId}.", subscriptionId);
                return StatusCode(500, "Error verifying subscription.");
            }

            var tier = plan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                "Admin" => SubscriptionTier.Admin,                
                _ => SubscriptionTier.Free
            };

            if (tier == SubscriptionTier.Free)
            {
                return BadRequest("Invalid plan for paid subscription.");
            }

            user.SubscriptionTier = tier;
            user.SubscriptionExpiry = DateTime.UtcNow.AddMonths(1);
            await _userManager.UpdateAsync(user);

            // Swap Identity role from Free to the paid tier
            var targetRole = tier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                _ => "Free"
            };
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, targetRole);

            var existingClaims = await _userManager.GetClaimsAsync(user);
            var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, tierClaim);
            }
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim("SubscriptionTier", tier.ToString()));

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Subscription successfully activated for {Email}, tier: {Tier}.", user.Email, tier);
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// PayPal callback endpoint after subscription approval
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> PaymentCallback(
            [FromQuery(Name = "subscription_id")] string subscriptionId,
            string plan)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(plan))
            {
                return RedirectToAction("MyAccount");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join");
            }

            try
            {
                var isValid = await _payPalService.VerifySubscription(subscriptionId);
                if (!isValid)
                {
                    return RedirectToAction("MyAccount");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify PayPal callback subscription with ID {SubscriptionId}.", subscriptionId);
                return RedirectToAction("MyAccount");
            }

            var tier = plan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

            user.SubscriptionTier = tier;
            user.SubscriptionExpiry = tier == SubscriptionTier.Free ? null : DateTime.UtcNow.AddMonths(1);
            await _userManager.UpdateAsync(user);

            // Swap Identity role from Free to the paid tier
            var targetRole = tier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                _ => "Free"
            };
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, targetRole);

            var existingClaims = await _userManager.GetClaimsAsync(user);
            var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, tierClaim);
            }
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim("SubscriptionTier", tier.ToString()));

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction("MyAccount");
        }
    }
}