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
        private readonly IStripeService _stripeService;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<MembershipController> logger,
            IMembershipService membershipService,
            IPayPalService payPalService,
            IStripeService stripeService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _membershipService = membershipService;
            _payPalService = payPalService;
            _stripeService = stripeService;
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

            // Stripe (credit card) checkout availability for this plan.
            ViewBag.StripeConfigured = _stripeService.IsConfigured
                && !string.IsNullOrWhiteSpace(_stripeService.GetPriceId(normalizedPlan));

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

            await GrantTierAsync(user, newTier, subscription.Id, subscription.NextBillingTime, PaymentProvider.PayPal);

            _logger.LogInformation("User {Email} activated {Tier} via PayPal subscription {SubscriptionId}.",
                user.Email, newTier, subscriptionId);

            return Json(new { redirectUrl = Url.Action("PaymentConfirmation", new { plan }) });
        }

        /// <summary>
        /// Creates a Stripe Checkout session for a paid plan and redirects the user to
        /// Stripe's hosted card-payment page.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStripeCheckout(string plan)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join");
            }

            var normalizedPlan = plan?.ToLower();
            if (normalizedPlan != "pro" && normalizedPlan != "allaccess")
            {
                return RedirectToAction("Join");
            }

            var successUrl = Url.Action("StripeSuccess", "Membership",
                new { plan = normalizedPlan, sessionId = "{CHECKOUT_SESSION_ID}" },
                Request.Scheme)!;
            // Stripe replaces the literal {CHECKOUT_SESSION_ID} placeholder itself, so undo URL-encoding.
            successUrl = successUrl.Replace("%7BCHECKOUT_SESSION_ID%7D", "{CHECKOUT_SESSION_ID}");
            var cancelUrl = Url.Action("Subscribe", "Membership", new { plan = normalizedPlan }, Request.Scheme)!;

            var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
                normalizedPlan, user.Email ?? string.Empty, successUrl, cancelUrl);

            if (string.IsNullOrWhiteSpace(checkoutUrl))
            {
                TempData["StripeError"] = "Card checkout is not available right now. Please try PayPal or contact support.";
                return RedirectToAction("Subscribe", new { plan = normalizedPlan });
            }

            return Redirect(checkoutUrl);
        }

        /// <summary>
        /// Stripe hosted checkout success callback. Verifies the subscription and, if
        /// active, grants the paid tier.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> StripeSuccess(string plan, string sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join");
            }

            var newTier = plan?.ToLower() switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

            if (newTier == SubscriptionTier.Free || string.IsNullOrWhiteSpace(sessionId))
            {
                return RedirectToAction("Subscribe", new { plan });
            }

            var subscription = await _stripeService.GetSubscriptionFromSessionAsync(sessionId);
            if (subscription == null || !subscription.IsActive)
            {
                _logger.LogWarning("Stripe session {SessionId} could not be verified for user {Email}.",
                    sessionId, user.Email);
                TempData["StripeError"] = "We could not verify your card payment. Please try again.";
                return RedirectToAction("Subscribe", new { plan });
            }

            await GrantTierAsync(user, newTier, subscription.Id, subscription.CurrentPeriodEnd, PaymentProvider.Stripe);

            _logger.LogInformation("User {Email} activated {Tier} via Stripe subscription {SubscriptionId}.",
                user.Email, newTier, subscription.Id);

            return RedirectToAction("PaymentConfirmation", new { plan });
        }

        /// <summary>
        /// Persists a paid tier, updates roles/claims and refreshes sign-in.
        /// </summary>
        private async Task GrantTierAsync(ApplicationUser user, SubscriptionTier newTier, string subscriptionId, DateTime? nextBillingTime, PaymentProvider provider)
        {
            var previousRole = user.SubscriptionTier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                _ => "Free"
            };

            user.SubscriptionTier = newTier;
            user.SubscriptionExpiry = nextBillingTime ?? DateTime.UtcNow.AddMonths(1);
            if (provider == PaymentProvider.Stripe)
            {
                user.StripeSubscriptionId = subscriptionId;
            }
            else
            {
                user.PayPalSubscriptionId = subscriptionId;
            }
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

            await GrantTierAsync(user, newTier, user.PayPalSubscriptionId ?? string.Empty, null, PaymentProvider.PayPal);

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

    /// <summary>
    /// Payment provider used to activate a paid membership.
    /// </summary>
    public enum PaymentProvider
    {
        PayPal = 0,
        Stripe = 1
    }
}