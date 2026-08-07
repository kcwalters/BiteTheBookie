using BiteTheBookie.Models;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Encodings.Web;

namespace BiteTheBookie.Controllers
{
    public class MembershipController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<MembershipController> _logger;

        public MembershipController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILogger<MembershipController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
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
        /// Admin-only diagnostic: sends a test email using the configured Email settings
        /// (SMTP) so you can verify delivery without going through registration.
        /// Usage: GET /Membership/SendTestEmail?to=you@yourdomain.com
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> SendTestEmail(string? to)
        {
            var recipient = string.IsNullOrWhiteSpace(to)
                ? (await _userManager.GetUserAsync(User))?.Email
                : to;

            if (string.IsNullOrWhiteSpace(recipient))
            {
                return BadRequest("No recipient specified. Pass ?to=address@example.com");
            }

            var subject = "BiteTheBookie SMTP test";
            var body =
                $@"<h2>SMTP test successful ?</h2>
<p>This is a test email sent from BiteTheBookie at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.</p>
<p>If you received this, your Email settings are working.</p>";

            try
            {
                await _emailSender.SendEmailAsync(recipient, subject, body);
                _logger.LogInformation("Test email dispatched to {Recipient}.", recipient);
                return Content($"Test email sent to {recipient}. Check the inbox (and the app logs).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test email to {Recipient} failed.", recipient);
                return StatusCode(500, $"Failed to send test email: {ex.Message}");
            }
        }

        /// <summary>
        /// Admin-only diagnostic: resets a user's password directly (no email link).
        /// Usage: GET /Membership/AdminResetPassword?email=you@example.com&newPassword=NewPass123
        /// The account is also marked email-confirmed so login isn't blocked.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminResetPassword(string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
            {
                return BadRequest("Pass ?email=address@example.com&newPassword=YourNewPassword");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound($"No account found for {email}.");
            }

            // Ensure the account can log in (confirmed) after the reset.
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Admin reset password for {Email}.", email);
                return Content($"Password reset for {email}. You can now log in with the new password.");
            }

            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Admin password reset failed for {Email}: {Errors}", email, errors);
            return BadRequest($"Password reset failed: {errors}");
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

            // If an account already exists for this email but was never confirmed,
            // resend the confirmation email instead of showing "username already taken".
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                if (!await _userManager.IsEmailConfirmedAsync(existingUser))
                {
                    await SendConfirmationEmailAsync(existingUser, model.SelectedPlan);
                    _logger.LogInformation(
                        "Resent confirmation email to unconfirmed account {Email}.", model.Email);
                    return RedirectToAction("RegisterConfirmation", new { email = model.Email });
                }

                ModelState.AddModelError(string.Empty,
                    "An account with this email already exists. Please log in instead.");
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
                    "premium" => SubscriptionTier.Premium,
                    "vip" => SubscriptionTier.VIP,
                    _ => SubscriptionTier.Free
                },
                SubscriptionExpiry = model.SelectedPlan?.ToLower() switch
                {
                    "premium" => DateTime.UtcNow.AddMonths(1),
                    "vip" => DateTime.UtcNow.AddMonths(1),
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
                    SubscriptionTier.VIP => "VIP",
                    SubscriptionTier.Premium => "Premium",
                    _ => "Free"
                };
                await _userManager.AddToRoleAsync(user, roleName);

                // Add subscription claim
                await _userManager.AddClaimAsync(user, 
                    new System.Security.Claims.Claim("SubscriptionTier", user.SubscriptionTier.ToString()));

                // ?? Send email confirmation link ??????????????????????????????
                await SendConfirmationEmailAsync(user, model.SelectedPlan);

                // Account must be confirmed before sign-in (RequireConfirmedAccount = true),
                // so redirect to a "check your email" page instead of signing in.
                return RedirectToAction("RegisterConfirmation", new { email = model.Email });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        /// <summary>Generates a confirmation token and emails the user a verification link.</summary>
        private async Task SendConfirmationEmailAsync(ApplicationUser user, string? plan)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var callbackUrl = Url.Action(
                action: "ConfirmEmail",
                controller: "Membership",
                values: new { userId = user.Id, token = encodedToken, plan },
                protocol: Request.Scheme)!;

            var htmlBody =
                $@"<p>Hi {HtmlEncoder.Default.Encode(user.FirstName ?? "there")},</p>
<p>Thanks for joining <strong>BiteTheBookie</strong>! Please confirm your email address to activate your account.</p>
<p><a href=""{HtmlEncoder.Default.Encode(callbackUrl)}"" style=""display:inline-block;padding:10px 18px;background:#0066cc;color:#fff;text-decoration:none;border-radius:6px;font-weight:600;"">Confirm my email</a></p>
<p>If the button doesn't work, copy and paste this link into your browser:<br/>{HtmlEncoder.Default.Encode(callbackUrl)}</p>
<p>If you didn't create this account, you can ignore this email.</p>";

            await _emailSender.SendEmailAsync(user.Email!, "Confirm your BiteTheBookie account", htmlBody);
        }

        /// <summary>Post-registration page telling the user to check their email.</summary>
        [HttpGet]
        public IActionResult RegisterConfirmation(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        /// <summary>Handles the confirmation link the user clicks in their email.</summary>
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token, string? plan)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewBag.Success = false;
                return View();
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            }
            catch (FormatException)
            {
                ViewBag.Success = false;
                return View();
            }

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            ViewBag.Success = result.Succeeded;

            if (result.Succeeded)
            {
                _logger.LogInformation("Email confirmed for {Email}.", user.Email);

                // Sign the user in now that their account is confirmed.
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Paid plans continue to payment; free plans go home.
                if (user.SubscriptionTier != SubscriptionTier.Free)
                {
                    ViewBag.RedirectPlan = plan;
                }
            }

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
        /// Change password form (self-service, for logged-in users)
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        /// <summary>
        /// Handle password change for the current user.
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join");
            }

            var result = await _userManager.ChangePasswordAsync(
                user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            // Keep the current user signed in with the updated credentials.
            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User {Email} changed their password.", user.Email);

            TempData["StatusMessage"] = "Your password has been changed.";
            return RedirectToAction("MyAccount");
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
                "premium" => SubscriptionTier.Premium,
                "vip" => SubscriptionTier.VIP,
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

            // Refresh sign-in to update claims
            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("MyAccount");
        }
    }
}