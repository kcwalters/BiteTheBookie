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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<MembershipController> _logger;
        private readonly PayPalService _payPalService;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            PayPalService payPalService,
            ILogger<MembershipController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
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
        /// <summary>
        /// On-site payment page: renders the PayPal subscribe button + a debit/credit card
        /// button so the user can subscribe without leaving the site or creating a PayPal account.
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult Payment(string plan)
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

            ViewBag.Plan = selectedPlan;
            ViewBag.PlanId = planId;
            ViewBag.ClientId = _payPalService.ClientId;
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

            // Lazily downgrade users whose paid access has lapsed (e.g. after cancelling).
            await EnsureSubscriptionCurrentAsync(user);

            var model = new MyAccountViewModel
            {
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                SubscriptionTier = user.SubscriptionTier,
                SubscriptionExpiry = user.SubscriptionExpiry,
                IsPro = user.IsPro,
                IsAllAccess = user.AllAccessUser,
                SubscriptionCancelled = user.SubscriptionCancelled,
                HasActivePaidSubscription = user.IsPro && !string.IsNullOrEmpty(user.PayPalSubscriptionId),
                CanUpgradeToAllAccess = user.SubscriptionTier == SubscriptionTier.Pro
                                        && user.IsPro
                                        && !user.SubscriptionCancelled
                                        && !string.IsNullOrEmpty(user.PayPalSubscriptionId),
                MemberSince = user.CreatedAt
            };

            return View(model);
        }

        /// <summary>
        /// Reverts a user to the Free tier/role once their paid access has expired. This is a
        /// lazy check (no background job) invoked when the user visits their account.
        /// </summary>
        private async Task EnsureSubscriptionCurrentAsync(ApplicationUser user)
        {
            var isPaidTier = user.SubscriptionTier == SubscriptionTier.Pro
                             || user.SubscriptionTier == SubscriptionTier.AllAccess;

            var expired = user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry.Value <= DateTime.UtcNow;

            if (isPaidTier && expired)
            {
                _logger.LogInformation("Paid access expired for {Email}; reverting to Free.", user.Email);
                user.PayPalSubscriptionId = null;
                user.SubscriptionCancelled = false;
                await ApplyTierAsync(user, SubscriptionTier.Free);
                await _signInManager.RefreshSignInAsync(user);
            }
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
                "admin" => SubscriptionTier.Admin,
                _ => SubscriptionTier.Free
            };

            if (tier == SubscriptionTier.Free)
            {
                return BadRequest("Invalid plan for paid subscription.");
            }

            // Persist the PayPal subscription id so we can later revise (upgrade) or cancel it.
            user.PayPalSubscriptionId = subscriptionId;
            user.SubscriptionCancelled = false;

            var applied = await ApplyTierAsync(user, tier);
            if (!applied)
            {
                return StatusCode(500, "Failed to activate subscription. Please contact support.");
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Subscription successfully activated for {Email}, tier: {Tier}.", user.Email, tier);
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// Upgrade page for existing Pro members: renders the PayPal button that revises the
        /// existing subscription to the AllAccess plan (same subscription id, new recurring amount).
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Upgrade()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Join");

            // Only active Pro members with a known PayPal subscription can upgrade.
            if (user.SubscriptionTier != SubscriptionTier.Pro || !user.IsPro || user.SubscriptionCancelled)
            {
                TempData["PaymentError"] = "Upgrades are only available to active Pro members.";
                return RedirectToAction("MyAccount");
            }

            if (string.IsNullOrEmpty(user.PayPalSubscriptionId))
            {
                _logger.LogWarning("Pro user {Email} has no stored PayPal subscription id; cannot upgrade.", user.Email);
                TempData["PaymentError"] = "We couldn't find your subscription details. Please contact support.";
                return RedirectToAction("MyAccount");
            }

            if (!_payPalService.IsConfigured)
            {
                _logger.LogWarning("PayPal is not configured; cannot render upgrade page.");
                TempData["PaymentError"] = "Online payments are not currently available. Please try again later.";
                return RedirectToAction("MyAccount");
            }

            var allAccessPlanId = _payPalService.GetPlanId("allaccess");
            if (string.IsNullOrWhiteSpace(allAccessPlanId))
            {
                _logger.LogWarning("No PayPal plan id configured for AllAccess.");
                TempData["PaymentError"] = "The AllAccess plan is not available right now. Please try again later.";
                return RedirectToAction("MyAccount");
            }

            ViewBag.ClientId = _payPalService.ClientId;
            ViewBag.PlanId = allAccessPlanId;
            ViewBag.SubscriptionId = user.PayPalSubscriptionId;

            return View();
        }

        /// <summary>
        /// Applies the AllAccess tier after PayPal confirms the subscription revision. The
        /// subscription id stays the same; only the plan/amount changes.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmUpgrade(string subscriptionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(subscriptionId)) return BadRequest("Missing subscription ID.");

            // Only an existing Pro member may complete an upgrade.
            if (user.SubscriptionTier != SubscriptionTier.Pro)
            {
                return BadRequest("Only Pro members can upgrade to AllAccess.");
            }

            try
            {
                var isValid = await _payPalService.VerifySubscription(subscriptionId);
                if (!isValid) return StatusCode(403, "Failed to verify the revised subscription.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify revised PayPal subscription {SubscriptionId}.", subscriptionId);
                return StatusCode(500, "Error verifying subscription.");
            }

            user.PayPalSubscriptionId = subscriptionId;
            user.SubscriptionCancelled = false;

            var applied = await ApplyTierAsync(user, SubscriptionTier.AllAccess);
            if (!applied)
            {
                return StatusCode(500, "Failed to apply the upgrade. Please contact support.");
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("Upgraded {Email} from Pro to AllAccess.", user.Email);
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// Confirmation page shown before cancelling a subscription.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Unsubscribe()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Join");

            if (!user.IsPro)
            {
                TempData["PaymentError"] = "You don't have an active paid subscription to cancel.";
                return RedirectToAction("MyAccount");
            }

            ViewBag.PlanName = user.SubscriptionTier == SubscriptionTier.AllAccess ? "AllAccess" : "Pro";
            ViewBag.AccessUntil = user.SubscriptionExpiry;
            ViewBag.AlreadyCancelled = user.SubscriptionCancelled;
            return View();
        }

        /// <summary>
        /// Cancels the recurring PayPal subscription. The member keeps paid access until their
        /// current billing period ends (SubscriptionExpiry), then reverts to Free.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSubscription()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!user.IsPro)
            {
                TempData["PaymentError"] = "You don't have an active paid subscription to cancel.";
                return RedirectToAction("MyAccount");
            }

            if (!string.IsNullOrEmpty(user.PayPalSubscriptionId))
            {
                try
                {
                    var cancelled = await _payPalService.CancelSubscription(user.PayPalSubscriptionId, "Cancelled by member.");
                    if (!cancelled)
                    {
                        _logger.LogError("PayPal cancel failed for {Email} (subscription {SubscriptionId}).", user.Email, user.PayPalSubscriptionId);
                        TempData["PaymentError"] = "We couldn't cancel your subscription with PayPal. Please try again or contact support.";
                        return RedirectToAction("MyAccount");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling PayPal subscription {SubscriptionId} for {Email}.", user.PayPalSubscriptionId, user.Email);
                    TempData["PaymentError"] = "Something went wrong cancelling your subscription. Please try again or contact support.";
                    return RedirectToAction("MyAccount");
                }
            }

            // Keep tier/role and access until the current period ends; just flag as cancelled.
            user.SubscriptionCancelled = true;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("{Email} cancelled recurring billing; access retained until {Expiry}.", user.Email, user.SubscriptionExpiry);
            TempData["PaymentMessage"] = "Your subscription has been cancelled. You'll keep access until the end of your current billing period.";
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// Applies the paid tier to the user's Identity roles and claims, replacing any
        /// existing subscription role with the single role that matches <paramref name="tier"/>.
        /// Every Identity operation result is checked and logged so a failed write to
        /// AspNetUserRoles surfaces instead of silently leaving the user on the wrong role.
        /// </summary>
        private async Task<bool> ApplyTierAsync(ApplicationUser user, SubscriptionTier tier)
        {
            var targetRole = tier switch
            {
                SubscriptionTier.AllAccess => "AllAccess",
                SubscriptionTier.Pro => "Pro",
                SubscriptionTier.Admin => "Admin",
                _ => "Free"
            };

            // Resolve the Identity role for the selected account type so we assign the correct
            // RoleId. The role is seeded at startup; create it defensively if it is somehow missing
            // so AddToRoleAsync can never silently fail to find it.
            var role = await _roleManager.FindByNameAsync(targetRole);
            if (role == null)
            {
                var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(targetRole));
                if (!createRoleResult.Succeeded)
                {
                    _logger.LogError("Failed to create missing role {Role}: {Errors}",
                        targetRole, string.Join("; ", createRoleResult.Errors.Select(e => e.Description)));
                    return false;
                }
                role = await _roleManager.FindByNameAsync(targetRole);
            }

            if (role == null)
            {
                _logger.LogError("Could not resolve role {Role} for {Email}; aborting tier assignment.", targetRole, user.Email);
                return false;
            }

            // Ensure the persisted SubscriptionTier is in sync with the role we are about to grant.
            // Always persist here so related fields set by the caller (e.g. PayPalSubscriptionId,
            // SubscriptionCancelled) are saved even when the tier itself is unchanged.
            if (user.SubscriptionTier != tier)
            {
                user.SubscriptionTier = tier;
                user.SubscriptionExpiry = tier == SubscriptionTier.Free ? null : DateTime.UtcNow.AddMonths(1);
            }
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Failed to update SubscriptionTier for {Email}: {Errors}",
                    user.Email, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                return false;
            }

            // Only the subscription/access roles are managed here; remove any of them the user
            // currently has that are not the target role, then add the target role if missing.
            var subscriptionRoles = new[] { "Free", "Pro", "AllAccess", "Admin" };
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles
                .Where(r => subscriptionRoles.Contains(r) && !string.Equals(r, targetRole, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    _logger.LogError("Failed to remove roles [{Roles}] from {Email}: {Errors}",
                        string.Join(", ", rolesToRemove), user.Email,
                        string.Join("; ", removeResult.Errors.Select(e => e.Description)));
                    return false;
                }
            }

            if (!await _userManager.IsInRoleAsync(user, targetRole))
            {
                var addResult = await _userManager.AddToRoleAsync(user, targetRole);
                if (!addResult.Succeeded)
                {
                    _logger.LogError("Failed to add role {Role} to {Email}: {Errors}",
                        targetRole, user.Email,
                        string.Join("; ", addResult.Errors.Select(e => e.Description)));
                    return false;
                }
            }

            // Keep the SubscriptionTier claim in sync with the granted role.
            var existingClaims = await _userManager.GetClaimsAsync(user);
            var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "SubscriptionTier");
            if (tierClaim != null && !string.Equals(tierClaim.Value, tier.ToString(), StringComparison.Ordinal))
            {
                await _userManager.RemoveClaimAsync(user, tierClaim);
                tierClaim = null;
            }
            if (tierClaim == null)
            {
                await _userManager.AddClaimAsync(user,
                    new System.Security.Claims.Claim("SubscriptionTier", tier.ToString()));
            }

            _logger.LogInformation("Applied tier {Tier} (role {Role}, roleId {RoleId}) to {Email}.", tier, targetRole, role.Id, user.Email);
            return true;
        }
    }
}