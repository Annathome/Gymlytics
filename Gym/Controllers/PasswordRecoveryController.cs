using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Gym.Controllers;

[AllowAnonymous]
public class PasswordRecoveryController(SetupService setup, HashingService hashing,
    GmailValidationService mail, IDataProtectionProvider protection, IConfiguration configuration) : Controller
{
    private readonly ITimeLimitedDataProtector protector = protection.CreateProtector("StaffPasswordRecovery.v1").ToTimeLimitedDataProtector();
    private static readonly ConcurrentDictionary<string, DateTime> Requests = new();
    [HttpGet]
    public IActionResult Index() => View(new PasswordRecoveryModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PasswordRecoveryModel model)
    {
        var email = model.Email?.Trim() ?? "";
        if (!new EmailAddressAttribute().IsValid(email))
        {
            ModelState.AddModelError("Email", "Enter a valid email address.");
            return View(model);
        }
        ViewBag.Message = "If an active account uses this email, a password reset link will be sent. Check your inbox and spam folder.";
        var key = email.ToLowerInvariant();
        var now = DateTime.UtcNow;
        foreach (var entry in Requests.Where(entry => entry.Value < now.AddMinutes(-5))) Requests.TryRemove(entry.Key, out _);
        if (!Requests.TryAdd(key, now)) return View(model);
        var account = await setup.FindPasswordResetAccountAsync(email);
        if (account.HasValue)
        {
            var token = protector.Protect(JsonSerializer.Serialize(new ResetData(account.Value.Id, account.Value.Hash, account.Value.Username)), TimeSpan.FromMinutes(30));
            var baseUrl = configuration["AppSettings:BaseUrl"]?.TrimEnd('/');
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                await mail.SendPasswordResetLinkAsync(email, baseUrl + Url.Action("Reset", "PasswordRecovery", new { token }));
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Reset(string token)
    {
        var model = new PasswordRecoveryModel { Token = token };
        try
        {
            var data = JsonSerializer.Deserialize<ResetData>(protector.Unprotect(token ?? "", out _));
            model.Username = data?.Username;
        }
        catch { }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(PasswordRecoveryModel model)
    {
        if (string.IsNullOrEmpty(model.Password) || model.Password.Length < 8 || model.Password.Length > 128)
            ModelState.AddModelError("Password", "Use 8–128 characters.");
        if (model.Password != model.ConfirmPassword) ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
        if (!ModelState.IsValid) return View(model);
        ResetData? data;
        try { data = JsonSerializer.Deserialize<ResetData>(protector.Unprotect(model.Token ?? "", out _)); }
        catch { data = null; }
        if (data == null || !await setup.CompletePasswordResetAsync(data.Id, data.Hash, hashing.HashPassword(model.Password!)))
        {
            ModelState.AddModelError("", "This reset link is invalid, expired, or already used. Request a new link.");
            return View(model);
        }
        ViewBag.Message = "Your password has been changed. You can now sign in with your new password.";
        return View(new PasswordRecoveryModel());
    }
    public record ResetData(int Id, string Hash, string? Username = null);
}
