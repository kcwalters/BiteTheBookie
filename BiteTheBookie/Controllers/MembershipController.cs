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
        private readonly IPayPalService _payPalService;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<MembershipController> logger,
            IMembershipService membershipService,
            IPayPalService payPalService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _membershipService = membershipService;
            _payPalService = payPalService;
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

            // Paid tiers are not granted until PayPal confirms the subscription.
            // Everyone is created as Free first; ConfirmSubscription promotes them.
            var desiredTier = model.SelectedPlan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

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
                SubscriptionTier = SubscriptionTier.Free,
                SubscriptionExpiry = null,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {Email} created a new account with plan {Plan}.", model.Email, model.SelectedPlan);

                // Start everyone in the Free role/claim; upgrade happens after payment.
                await _userManager.AddToRoleAsync(user, "Free");
                await _userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("SubscriptionTier", SubscriptionTier.Free.ToString()));

                _membershipService.UpdateMembershipLevel(user.Id, SubscriptionTier.Free);

                await _signInManager.SignInAsync(user, isPersistent: false);

                // Paid plan -> send to PayPal subscription checkout to collect payment.
                if (desiredTier != SubscriptionTier.Free)
                {
                    return RedirectToAction("Subscribe", new { plan = model.SelectedPlan });
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
        /// PayPal subscription checkout page (Smart Buttons) for a paid plan.
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult Subscribe(string plan)
        {
            var normalizedPlan = plan?.ToLower();
            if (normalizedPlan != "pro" && normalizedPlan != "allaccess")
            {
                return RedirectToAction("Join");
            }

            var planId = _payPalService.GetPlanId(normalizedPlan);
            var clientId = _payPalService.ClientId;

            ViewBag.Plan = normalizedPlan;
            ViewBag.PayPalClientId = clientId;
            ViewBag.PayPalPlanId = planId;
            ViewBag.IsConfigured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(planId);

            return View();
        }

        /// <summary>
        /// Verifies a PayPal subscription approved in the browser and, if active,
        /// grants the paid tier. Called via AJAX from the Subscribe view.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubscription(string plan, string subscriptionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            var newTier = plan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

            if (newTier == SubscriptionTier.Free || string.IsNullOrWhiteSpace(subscriptionId))
            {
                return BadRequest(new { message = "Invalid subscription request." });
            }

            var subscription = await _payPalService.GetSubscriptionAsync(subscriptionId);
            if (subscription == null || !subscription.IsActive)
            {
                _logger.LogWarning("PayPal subscription {SubscriptionId} could not be verified for user {Email}.",
                    subscriptionId, user.Email);
                return BadRequest(new { message = "We could not verify your PayPal subscription. Please try again." });
            }

            await GrantTierAsync(user, newTier, subscription);

            _logger.LogInformation("User {Email} activated {Tier} via PayPal subscription {SubscriptionId}.",
                user.Email, newTier, subscriptionId);

            return Json(new { redirectUrl = Url.Action("PaymentConfirmation", new { plan }) });
        }

        /// <summary>
        /// Persists a paid tier, updates roles/claims and refreshes sign-in.
        /// </summary>
        private async Task GrantTierAsync(ApplicationUser user, SubscriptionTier newTier, PayPalSubscriptionResult subscription)
        {
            var previousRole = user.SubscriptionTier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                _ => "Free"
            };

            user.SubscriptionTier = newTier;
            user.SubscriptionExpiry = subscription.NextBillingTime ?? DateTime.UtcNow.AddMonths(1);
            user.PayPalSubscriptionId = subscription.Id;
            await _userManager.UpdateAsync(user);

            var newRole = newTier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                _ => "Free"
            };

            if (!string.Equals(previousRole, newRole, StringComparison.Ordinal))
            {
                if (await _userManager.IsInRoleAsync(user, previousRole))
                {
                    await _userManager.RemoveFromRoleAsync(user, previousRole);
                }
                if (!await _userManager.IsInRoleAsync(user, newRole))
                {
                    await _userManager.AddToRoleAsync(user, newRole);
                }
            }

            var existingClaims = await _userManager.GetClaimsAsync(user);
            var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, tierClaim);
            }
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim("SubscriptionTier", newTier.ToString()));

            _membershipService.UpdateMembershipLevel(user.Id, newTier);

            await _signInManager.RefreshSignInAsync(user);
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
        /// Upgrade subscription - routes through PayPal checkout to collect payment.
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

            // Paid tiers require a verified PayPal subscription first.
            if (newTier != SubscriptionTier.Free)
            {
                return RedirectToAction("Subscribe", new { plan });
            }

            await GrantTierAsync(user, newTier, new PayPalSubscriptionResult
            {
                Id = user.PayPalSubscriptionId ?? string.Empty,
                Status = "ACTIVE"
            });

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