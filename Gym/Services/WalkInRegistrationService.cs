using System.ComponentModel.DataAnnotations;
using Gym.Data;
using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services;

public sealed class WalkInRegistrationService(GymDbContext context)
{
    public async Task<WalkInClient> RegisterAsync(WalkInRegistrationViewModel form)
    {
        // Both portals use this boundary, even when browser validation is bypassed.
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), errors, true);
        var inputErrors = errors.Where(error => !error.MemberNames.Contains(nameof(form.AmountPaid))).ToList();
        if (inputErrors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", inputErrors.Select(error => error.ErrorMessage)));

        var rate = await context.WalkInPaymentTypes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == form.PaymentTypeId && item.IsActive);
        if (rate == null)
            throw new InvalidOperationException("Select an active walk-in payment type.");
        form.AmountPaid = rate.Amount;
        errors.Clear();
        if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
            throw new InvalidOperationException(string.Join(" ", errors.Select(error => error.ErrorMessage)));

        var firstName = RegistrationName.Normalize(form.FirstName);
        var lastName = RegistrationName.Normalize(form.LastName);
        var existingNames = await context.WalkInClients.AsNoTracking()
            .Select(item => item.FirstName + " " + item.LastName).ToListAsync();
        if (existingNames.Any(name => RegistrationName.Same(name, firstName + " " + lastName)))
            throw new InvalidOperationException("A walk-in with this full name is already registered. Use the existing client's return-visit action.");

        var email = form.Email.Trim();
        var contact = form.ContactNumber.Trim();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        if (await context.WalkInClients.AsNoTracking().AnyAsync(item =>
            item.RegisteredAt >= today && item.RegisteredAt < tomorrow &&
            (item.Email == email || item.ContactNumber == contact)))
            throw new InvalidOperationException("This walk-in client has already been registered today. Registration is available again tomorrow.");

        // Payments pages derive walk-in payments from this row. Never add it
        // until every required input and business rule has passed validation.
        var client = new WalkInClient
        {
            FirstName = firstName, LastName = lastName, ContactNumber = contact,
            Email = email, PaymentTypeName = rate.Name, ReasonForVisit = form.ReasonForVisit.Trim(),
            PaymentMethod = "Cash", AmountPaid = rate.Amount, IssueDigitalReceipt = true,
            LiabilityAccepted = false, Status = "Checked-in", RegisteredAt = DateTime.Now
        };
        var saved = false;
        try
        {
            context.WalkInClients.Add(client);
            await context.SaveChangesAsync();
            saved = true;
            return client;
        }
        finally
        {
            if (!saved)
                context.Entry(client).State = EntityState.Detached;
        }
    }
}
