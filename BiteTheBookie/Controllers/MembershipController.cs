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

            if (_signInManager.IsSignedIn(User))
            {
                // Existing members don't re-register; route them to the subscribe/upgrade flow
                // so their current account is reused.
                return RedirectToAction("Subscribe", new { plan = selectedPlan });
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

            if (_signInManager.IsSignedIn(User))
            {
                // Existing members reuse their account instead of registering a new one.
                return RedirectToAction("Subscribe", new { plan = model.SelectedPlan?.ToLowerInvariant() });
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

            SetCheckoutContext(new CheckoutContext
            {
                Plan = selectedPlan,
                Mode = CheckoutMode.NewAccount
            });

            _logger.LogInformation("Captured pending registration for {Email} (plan {Plan}); awaiting PayPal payment.", model.Email, selectedPlan);

            return RedirectToAction("Payment");
        }

        /// <summary>
        /// Lightweight confirmation page shown before we send the user to PayPal. It reads the
        /// in-progress checkout from session and presents a "Proceed to PayPal" button that posts
        /// to <see cref="StartCheckout"/>. Works for new sign-ups, existing members subscribing,
        /// and Pro-to-AllAccess upgrades.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Payment()
        {
            var context = GetCheckoutContext();
            if (context == null)
            {
                TempData["PaymentError"] = "Your session expired. Please start again.";
                return RedirectToAction("Join");
            }

            var selectedPlan = context.Plan;
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

            // Verify the plan id exists (and is ACTIVE) in the PayPal account tied to the configured
            // credentials, so we can show a clear message instead of failing later at PayPal.
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
            ViewBag.PlanName = selectedPlan == "allaccess" ? "All Access" : "Pro";
            ViewBag.PlanPrice = selectedPlan == "allaccess" ? "$19.99" : "$9.99";
            ViewBag.IsUpgrade = context.Mode == CheckoutMode.Upgrade;

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
        /// Lets a signed-in member with no active paid subscription start a new subscription using
        /// their existing account (no re-registration). Payment happens on PayPal; the tier/role is
        /// applied only after PayPal approves.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Subscribe(string? plan)
        {
            var selectedPlan = plan?.ToLowerInvariant();
            if (selectedPlan != "pro" && selectedPlan != "allaccess")
            {
                return RedirectToAction("Join");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Join");

            // Members with an active paid subscription upgrade or manage instead of subscribing again.
            if (user.IsPro && !user.SubscriptionCancelled)
            {
                if (user.SubscriptionTier == SubscriptionTier.Pro && selectedPlan == "allaccess")
                {
                    return RedirectToAction("Upgrade");
                }

                TempData["PaymentError"] = "You already have an active subscription. You can manage it from My Account.";
                return RedirectToAction("MyAccount");
            }

            // Clear any stale pending-registration from a previous anonymous attempt.
            HttpContext.Session.Remove(PendingRegistration.SessionKey);
            SetCheckoutContext(new CheckoutContext { Plan = selectedPlan, Mode = CheckoutMode.ExistingAccount });
            return RedirectToAction("Payment");
        }

        /// <summary>
        /// Creates (or revises, for an upgrade) the PayPal subscription server-side and redirects the
        /// browser to PayPal's approval page so the user can pay on PayPal's site. PayPal then
        /// redirects back to <see cref="CheckoutReturn"/>.
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartCheckout()
        {
            var context = GetCheckoutContext();
            if (context == null)
            {
                TempData["PaymentError"] = "Your checkout session expired. Please start again.";
                return RedirectToAction("Join");
            }

            if (!_payPalService.IsConfigured)
            {
                _logger.LogWarning("PayPal is not configured; cannot start checkout for plan {Plan}.", context.Plan);
                TempData["PaymentError"] = "Online payments are not currently available. Please try again later.";
                return RedirectToAction("Join");
            }

            var planId = _payPalService.GetPlanId(context.Plan);
            if (string.IsNullOrWhiteSpace(planId))
            {
                _logger.LogWarning("No PayPal plan id configured for plan {Plan}.", context.Plan);
                TempData["PaymentError"] = "This plan is not available right now. Please try again later.";
                return RedirectToAction("Join");
            }

            var returnUrl = Url.Action("CheckoutReturn", "Membership", null, Request.Scheme)!;
            var cancelUrl = Url.Action("CheckoutCancel", "Membership", null, Request.Scheme)!;

            try
            {
                PayPalSubscriptionResult result;
                if (context.Mode == CheckoutMode.Upgrade)
                {
                    if (string.IsNullOrWhiteSpace(context.SubscriptionId))
                    {
                        _logger.LogWarning("Upgrade checkout started without an existing subscription id.");
                        TempData["PaymentError"] = "We couldn't find your subscription to upgrade. Please contact support.";
                        return RedirectToAction("MyAccount");
                    }

                    result = await _payPalService.ReviseSubscriptionAsync(context.SubscriptionId, planId, returnUrl, cancelUrl);
                }
                else
                {
                    result = await _payPalService.CreateSubscriptionAsync(planId, returnUrl, cancelUrl);
                }

                context.SubscriptionId = result.SubscriptionId;
                SetCheckoutContext(context);

                // When PayPal doesn't require buyer approval (some revisions), it applies the change
                // immediately and returns no approve link; provision right away in that case.
                if (string.IsNullOrWhiteSpace(result.ApproveUrl))
                {
                    return await CheckoutReturn(result.SubscriptionId);
                }

                return Redirect(result.ApproveUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start PayPal checkout for plan {Plan} (mode {Mode}).", context.Plan, context.Mode);
                TempData["PaymentError"] = "We couldn't start your PayPal checkout. Please try again or contact support.";
                return RedirectToAction("Payment");
            }
        }

        /// <summary>
        /// PayPal redirects the user back here after they approve payment. We verify the subscription
        /// is active, then create the account + role (new users) or apply the tier/role to the
        /// existing account (existing members / upgrades), and sign them in.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CheckoutReturn(string? subscription_id)
        {
            var context = GetCheckoutContext();
            if (context == null)
            {
                TempData["PaymentError"] = "Your checkout session expired before we could finish. If you were charged, please contact support.";
                return RedirectToAction("Join");
            }

            // New subscriptions come back with subscription_id on the query string; a revise keeps the
            // id we already stored.
            var subscriptionId = !string.IsNullOrWhiteSpace(subscription_id) ? subscription_id : context.SubscriptionId;
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogWarning("CheckoutReturn had no subscription id (mode {Mode}).", context.Mode);
                TempData["PaymentError"] = "We didn't receive your payment confirmation from PayPal. If you were charged, please contact support.";
                return RedirectToAction("Join");
            }

            var tier = context.Plan switch
            {
                "pro" => SubscriptionTier.Pro,
                "allaccess" => SubscriptionTier.AllAccess,
                _ => SubscriptionTier.Free
            };
            if (tier == SubscriptionTier.Free)
            {
                _logger.LogWarning("CheckoutReturn received an invalid paid plan '{Plan}'.", context.Plan);
                TempData["PaymentError"] = "That plan isn't valid for a paid subscription. Please try again.";
                return RedirectToAction("Join");
            }

            // Verify the subscription is genuinely active/approved before provisioning anything.
            try
            {
                if (!await _payPalService.VerifySubscription(subscriptionId))
                {
                    _logger.LogWarning("PayPal subscription {SubscriptionId} did not verify as active (mode {Mode}).", subscriptionId, context.Mode);
                    TempData["PaymentError"] = "We couldn't confirm your subscription with PayPal yet. If you were charged, it may take a moment to activate — please refresh or contact support.";
                    return RedirectToAction("Payment");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify PayPal subscription {SubscriptionId}.", subscriptionId);
                TempData["PaymentError"] = "Something went wrong confirming your subscription. If you were charged, please contact support.";
                return RedirectToAction("Payment");
            }

            return context.Mode == CheckoutMode.NewAccount
                ? await CompleteNewAccountAsync(subscriptionId, tier)
                : await CompleteExistingAccountAsync(subscriptionId, tier);
        }

        /// <summary>
        /// Handles the PayPal "cancel" redirect: the user backed out before paying.
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        public IActionResult CheckoutCancel()
        {
            TempData["PaymentError"] = "Checkout was cancelled before payment was completed.";
            return RedirectToAction("Payment");
        }

        /// <summary>
        /// Creates a brand-new account + role from the pending registration held in session, then
        /// signs the new member in. No account exists before this point.
        /// </summary>
        private async Task<IActionResult> CompleteNewAccountAsync(string subscriptionId, SubscriptionTier tier)
        {
            var pending = GetPendingRegistration();
            if (pending == null)
            {
                _logger.LogWarning("CompleteNewAccount called with no pending registration (subscription {SubscriptionId}).", subscriptionId);
                TempData["PaymentError"] = "Your session expired before we could finish. Please register again. If you were charged, please contact support.";
                return RedirectToAction("Join");
            }

            // Guard against a duplicate account (e.g. double submit or an account created meanwhile).
            var existing = await _userManager.FindByEmailAsync(pending.Email);
            if (existing != null)
            {
                _logger.LogWarning("Account already exists for {Email} at checkout return; not creating a duplicate.", pending.Email);
                ClearCheckoutSession();
                TempData["PaymentError"] = "An account with this email already exists. Please log in.";
                return RedirectToAction("Login", "Account");
            }

            // Payment confirmed — NOW create the account (AspNetUsers) and role (AspNetUserRoles).
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

            ClearCheckoutSession();
            await _signInManager.SignInAsync(user, isPersistent: false);

            _logger.LogInformation("Account created and subscription activated for {Email}, tier: {Tier}.", user.Email, tier);
            TempData["PaymentMessage"] = $"Your {(tier == SubscriptionTier.AllAccess ? "All Access" : "Pro")} membership is now active. Welcome aboard!";
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// Applies the paid tier/role to the currently signed-in account (a Free member subscribing or
        /// a Pro member upgrading). The existing account is reused — nothing new is inserted into
        /// AspNetUsers.
        /// </summary>
        private async Task<IActionResult> CompleteExistingAccountAsync(string subscriptionId, SubscriptionTier tier)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("CompleteExistingAccount called but no user is signed in (subscription {SubscriptionId}).", subscriptionId);
                TempData["PaymentError"] = "Please log in to finish activating your subscription. If you were charged, please contact support.";
                return RedirectToAction("Login", "Account");
            }

            user.PayPalSubscriptionId = subscriptionId;
            user.SubscriptionCancelled = false;

            var applied = await ApplyTierAsync(user, tier);
            if (!applied)
            {
                _logger.LogError("Failed to apply tier {Tier} for {Email} after PayPal subscription {SubscriptionId} verified.", tier, user.Email, subscriptionId);
                TempData["PaymentError"] = "Your payment went through but we couldn't activate your plan. Please contact support and we'll fix it right away.";
                return RedirectToAction("MyAccount");
            }

            ClearCheckoutSession();
            await _signInManager.RefreshSignInAsync(user);

            _logger.LogInformation("Applied tier {Tier} to existing account {Email} (subscription {SubscriptionId}).", tier, user.Email, subscriptionId);
            TempData["PaymentMessage"] = $"Your {(tier == SubscriptionTier.AllAccess ? "All Access" : "Pro")} membership is now active.";
            return RedirectToAction("MyAccount");
        }

        /// <summary>
        /// Reads the in-progress checkout context from session; null when absent/invalid.
        /// </summary>
        private CheckoutContext? GetCheckoutContext()
        {
            var json = HttpContext.Session.GetString(CheckoutContext.SessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<CheckoutContext>(json);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        private void SetCheckoutContext(CheckoutContext context)
            => HttpContext.Session.SetString(CheckoutContext.SessionKey,
                System.Text.Json.JsonSerializer.Serialize(context));

        private void ClearCheckoutSession()
        {
            HttpContext.Session.Remove(CheckoutContext.SessionKey);
            HttpContext.Session.Remove(PendingRegistration.SessionKey);
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
                _logger.LogWarning("PayPal is not configured; cannot start upgrade.");
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

            // Revise the existing subscription (same id) to All Access; the actual PayPal approval
            // and redirect happen from the Payment page via StartCheckout.
            SetCheckoutContext(new CheckoutContext
            {
                Plan = "allaccess",
                Mode = CheckoutMode.Upgrade,
                SubscriptionId = user.PayPalSubscriptionId
            });

            return RedirectToAction("Payment");
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
            user.SubscriptionTier = tier;
            // Always (re)set the expiry window for paid tiers. This must run even when the tier value
            // is unchanged (e.g. a brand-new account whose tier was set at construction, or a renewal),
            // otherwise SubscriptionExpiry stays null and IsPro/AllAccessUser evaluate to false even
            // though the paid role/claim were granted.
            user.SubscriptionExpiry = tier == SubscriptionTier.Free ? null : DateTime.UtcNow.AddMonths(1);
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