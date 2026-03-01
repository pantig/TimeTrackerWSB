using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeTrackerApp.Data;
using TimeTrackerApp.Models;
using TimeTrackerApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace TimeTrackerApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToProperDashboard();
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);

            if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Nieprawidłowy email lub hasło");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties 
            { 
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity), authProperties);

            // Sprawdzenie czy returnUrl nie jest złośliwym przekierowaniem i czy nie jest puste
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.Contains("/Account/Login"))
            {
                return Redirect(returnUrl);
            }

            return RedirectToProperDashboard(user.Role);
        }

        private IActionResult RedirectToProperDashboard(UserRole? role = null)
        {
            if (role == null)
            {
                var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
                if (!string.IsNullOrEmpty(roleClaim) && Enum.TryParse<UserRole>(roleClaim, out var r))
                {
                    role = r;
                }
            }

            if (role == UserRole.Admin || role == UserRole.Manager)
            {
                return Redirect("/Employees/Index");
            }

            return Redirect("/TimeEntries/Index");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Registration failed - invalid model state");
                return View(model);
            }

            // Check if email already exists
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                _logger.LogWarning($"Registration failed - email already exists: {model.Email}");
                ModelState.AddModelError("Email", "Adres email jest już zarejestrowany");
                return View(model);
            }

            try
            {
                var user = new User
                {
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Role = UserRole.Employee,
                    IsActive = true
                };

                _logger.LogInformation($"Creating new user: {user.Email}");
                _context.Users.Add(user);
                
                var changesSaved = await _context.SaveChangesAsync();
                _logger.LogInformation($"User created successfully. Changes saved: {changesSaved}");
                
                // ✅ FIXED: Verify user was actually saved
                var verifyUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (verifyUser == null)
                {
                    _logger.LogError($"User creation verification failed - user not found in database: {model.Email}");
                    ModelState.AddModelError("", "Błąd podczas tworzenia konta. Spróbuj ponownie.");
                    return View(model);
                }
                
                _logger.LogInformation($"User verified in database. ID: {verifyUser.Id}");
                
                // ✅ FIXED: Don't auto-login, redirect to login page
                TempData["SuccessMessage"] = "Konto zostało utworzone. Możesz się teraz zalogować.";
                return RedirectToAction("Login");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Database error during user registration: {model.Email}");
                ModelState.AddModelError("", "Błąd bazy danych. Spróbuj ponownie.");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during user registration: {model.Email}");
                ModelState.AddModelError("", "Wystąpił nieoczekiwany błąd. Spróbuj ponownie.");
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
