using System.ComponentModel.DataAnnotations;

namespace Gym.Models;

public sealed class MinimumRegistrationAgeAttribute : ValidationAttribute
{
    public MinimumRegistrationAgeAttribute() : base("Registrant must be at least 13 years old.") { }

    public static bool IsEligible(DateTime birthDate, DateTime today)
    {
        birthDate = birthDate.Date;
        today = today.Date;
        if (birthDate == default || birthDate > today) return false;

        var age = today.Year - birthDate.Year;
        if (birthDate.Month > today.Month ||
            (birthDate.Month == today.Month && birthDate.Day > today.Day)) age--;
        return age >= 13;
    }

    public override bool IsValid(object? value) => value is null ||
        value is DateTime date && IsEligible(date, DateTime.Today);
}
