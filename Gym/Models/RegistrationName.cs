using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Gym.Models;

public static class RegistrationName
{
    public static string Normalize(string? value) => Regex.Replace((value ?? "").Trim(), @"\s+", " ");
    public static bool ValidPart(string? value)
    {
        var name = Normalize(value);
        return name.Length >= 2 && name.Any(char.IsLetter) &&
            Regex.IsMatch(name, @"^[\p{L}\p{M} '\-’]+$") &&
            !new[] { "na", "none", "unknown", "null" }.Contains(name.ToLowerInvariant());
    }
    public static bool ValidFull(string? value) => Normalize(value).Split(' ').Length >= 2 &&
        Normalize(value).Split(' ').All(ValidPart);
    public static bool Same(string? left, string? right) => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}

public sealed class CompleteRegistrationNameAttribute : ValidationAttribute
{
    public bool FullName { get; set; }
    public CompleteRegistrationNameAttribute() : base("Enter a complete name using letters, not initials or placeholders such as N/A.") { }
    public override bool IsValid(object? value) => FullName ? RegistrationName.ValidFull(value as string) : RegistrationName.ValidPart(value as string);
}
