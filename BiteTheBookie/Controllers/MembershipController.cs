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
            var selectedPlan = plan?.ToLowerInvariant();
            if (selectedPlan != "pro" && selectedPlan != "allaccess")
            {
                // No free tier: a valid paid plan must be chosen before registering.
                return RedirectToAction("Join");
            }

            var model = new RegisterViewModel
            {
                SelectedPlan = selectedPlan
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

            var selectedPlan = model.SelectedPlan?.ToLowerInvariant();
            if (selectedPlan != "pro" && selectedPlan != "allaccess")
            {
                // No free tier: only paid plans can be registered.
                ModelState.AddModelError(string.Empty, "Please choose a Pro or All Access plan.");
                return View(model);
            }

            if (!_payPalService.IsConfigured)
            {
                _logger.LogWarning("PayPal is not configured; cannot start subscription for plan {Plan}.", selectedPlan);
                ModelState.AddModelError(string.Empty, "Online payments are not currently available. Please try again later.");
                return View(model);
            }

            // Reject duplicate emails up front so the user isn't sent through payment
            // only to fail account creation afterward.
            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email already exists. Please log in instead.");
                return View(model);
            }

            // Do NOT create the account yet. Stash the registration details in session and
            // only create the user after PayPal approves the subscription payment.
            var pending = new PendingRegistration
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                DateOfBirth = model.DateOfBirth,
                StreetAddress = model.StreetAddress,
                City = model.City,
                State = model.State,
                ZipCode = model.ZipCode,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email,
                Password = model.Password,
                Plan = selectedPlan
            };

            HttpContext.Session.SetString(PendingRegistration.SessionKey,
                System.Text.Json.JsonSerializer.Serialize(pending));

            _logger.LogInformation("Captured pending registration for {Email} (plan {Plan}); awaiting PayPal payment.", model.Email, selectedPlan);

            return RedirectToAction("Payment", new { plan = selectedPlan });
        }

        /// <summary>
        /// <summary>
        /// On-site payment page: renders the PayPal subscribe button so a prospective member
        /// can pay before an account exists. The plan comes from the pending registration held
        /// in session; the account is only created after PayPal approves the payment.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Payment(string plan)
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                TempData["PaymentError"] = "Your session expired. Please start your registration again.";
                return RedirectToAction("Join");
            }

            // The plan is authoritative from the pending registration, not the query string.
            var selectedPlan = pending.Plan;
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

            // Verify the plan id actually exists (and is ACTIVE) in the PayPal account tied to the
            // configured credentials. Without this, a plan id that belongs to a different account or
            // environment surfaces later as a cryptic "subscriptions#RESOURCE_NOT_FOUND" error in the
            // browser when the PayPal button is clicked. Catching it here gives the user a clear message
            // and logs an actionable server-side error for the operator.
            if (!await _payPalService.IsPlanActiveAsync(planId))
            {
                _logger.LogError(
                    "PayPal plan id '{PlanId}' for plan {Plan} could not be verified against the configured PayPal account. " +
                    "Ensure PayPal:PlanId matches the account/environment of PayPal:ClientId (re-run create-paypal-plans.ps1 with the live credentials and update configuration).",
                    planId, selectedPlan);
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
        /// Reads the pending registration (captured on the sign-up form) from session.
        /// Returns null if none is present or it can't be deserialized.
        /// </summary>
        private PendingRegistration? GetPendingRegistration()
        {
            var json = HttpContext.Session.GetString(PendingRegistration.SessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<PendingRegistration>(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
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
        /// Revokes paid access once a subscription has lapsed (e.g. after cancelling). There is
        /// no Free tier, so the paid role and SubscriptionTier claim are simply removed. This is a
        /// lazy check (no background job) invoked when the user visits their account.
        /// </summary>
        private async Task EnsureSubscriptionCurrentAsync(ApplicationUser user)
        {
            var isPaidTier = user.SubscriptionTier == SubscriptionTier.Pro
                             || user.SubscriptionTier == SubscriptionTier.AllAccess;

            var expired = user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry.Value <= DateTime.UtcNow;

            if (isPaidTier && expired)
            {
                _logger.LogInformation("Paid access expired for {Email}; revoking access.", user.Email);
                user.PayPalSubscriptionId = null;
                user.SubscriptionCancelled = false;
                user.SubscriptionExpiry = null;
                // Free is used only as an internal "no paid access" sentinel; it is not a
                // sign-up tier and grants no roles.
                user.SubscriptionTier = SubscriptionTier.Free;
                await _userManager.UpdateAsync(user);

                // Remove any subscription/access roles the user currently holds.
                var subscriptionRoles = new[] { "Pro", "AllAccess" };
                var currentRoles = await _userManager.GetRolesAsync(user);
                var rolesToRemove = currentRoles.Where(r => subscriptionRoles.Contains(r)).ToList();
                if (rolesToRemove.Count > 0)
                {
                    await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                }

                // Drop the SubscriptionTier claim so paid-only policies no longer pass.
                var claims = await _userManager.GetClaimsAsync(user);
                var tierClaim = claims.FirstOrDefault(c => c.Type == "SubscriptionTier");
                if (tierClaim != null)
                {
                    await _userManager.RemoveClaimAsync(user, tierClaim);
                }

                await _signInManager.RefreshSignInAsync(user);
            }
        }

        /// <summary>
        /// Validate the PayPal subscription and, once payment is confirmed, create the account
        /// from the pending registration held in session, assign the paid role, and sign the
        /// new member in. No account exists before this point.
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmSubscription(string subscriptionId, string plan)
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                _logger.LogWarning("ConfirmSubscription called with no pending registration in session (subscription {SubscriptionId}).", subscriptionId);
                TempData["PaymentError"] = "Your session expired before we could finish. Please register again. If you were charged, please contact support.";
                return RedirectToAction("Join");
            }

            var selectedPlan = pending.Plan;

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogWarning("ConfirmSubscription called without a subscription id for {Email} (plan {Plan}).", pending.Email, selectedPlan);
                TempData["PaymentError"] = "We didn't receive your payment confirmation from PayPal. If you were charged, please contact support.";
                return RedirectToAction("Payment");
            }

            var tier = selectedPlan switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };

            if (tier == SubscriptionTier.Free)
            {
                _logger.LogWarning("ConfirmSubscription received an invalid paid plan '{Plan}' for {Email}.", selectedPlan, pending.Email);
                TempData["PaymentError"] = "That plan isn't valid for a paid subscription. Please try again.";
                return RedirectToAction("Join");
            }

            // Verify the subscription is real and active before creating any account.
            try
            {
                var isValid = await _payPalService.VerifySubscription(subscriptionId);
                if (!isValid)
                {
                    _logger.LogWarning("PayPal subscription {SubscriptionId} for {Email} did not verify as active.", subscriptionId, pending.Email);
                    TempData["PaymentError"] = "We couldn't confirm your subscription with PayPal yet. If you were charged, it may take a moment to activate — please refresh or contact support.";
                    return RedirectToAction("Payment");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify PayPal subscription with ID {SubscriptionId}.", subscriptionId);
                TempData["PaymentError"] = "Something went wrong confirming your subscription. If you were charged, please contact support.";
                return RedirectToAction("Payment");
            }

            // Guard against a duplicate account (e.g. double submit or an account created meanwhile).
            var existing = await _userManager.FindByEmailAsync(pending.Email);
            if (existing != null)
            {
                _logger.LogWarning("Account already exists for {Email} at ConfirmSubscription; not creating a duplicate.", pending.Email);
                HttpContext.Session.Remove(PendingRegistration.SessionKey);
                TempData["PaymentError"] = "An account with this email already exists. Please log in.";
                return RedirectToAction("Login", "Account");
            }

            // Payment confirmed — NOW create the account.
            var user = new ApplicationUser
            {
                UserName = pending.Email,
                Email = pending.Email,
                FirstName = pending.FirstName,
                LastName = pending.LastName,
                DateOfBirth = pending.DateOfBirth,
                StreetAddress = pending.StreetAddress,
                City = pending.City,
                State = pending.State,
                ZipCode = pending.ZipCode,
                PhoneNumber = pending.PhoneNumber,
                SubscriptionTier = tier,
                PayPalSubscriptionId = subscriptionId,
                SubscriptionCancelled = false,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, pending.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogError("Payment confirmed but account creation failed for {Email}: {Errors}",
                    pending.Email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
                TempData["PaymentError"] = "Your payment went through but we couldn't create your account. Please contact support and we'll fix it right away.";
                return RedirectToAction("Join");
            }

            var applied = await ApplyTierAsync(user, tier);
            if (!applied)
            {
                _logger.LogError("Failed to apply tier {Tier} for {Email} after PayPal subscription {SubscriptionId} verified.", tier, user.Email, subscriptionId);
                TempData["PaymentError"] = "Your payment went through but we couldn't activate your plan. Please contact support and we'll fix it right away.";
                return RedirectToAction("Login", "Account");
            }

            // Registration complete — clear the pending data and sign the new member in.
            HttpContext.Session.Remove(PendingRegistration.SessionKey);
            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Account created and subscription activated for {Email}, tier: {Tier}.", user.Email, tier);
            TempData["PaymentMessage"] = $"Your {(tier == SubscriptionTier.AllAccess ? "All Access" : "Pro")} membership is now active. Welcome aboard!";
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