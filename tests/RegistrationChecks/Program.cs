using Gym.Data;
using Gym.Models;
using Gym.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Gym.Controllers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<GymDbContext>().UseSqlite(connection).Options;
await using var db = new RegistrationContext(options);
await db.Database.EnsureCreatedAsync();
db.MembershipPlanSettings.Add(new MembershipPlanSetting
{
    Plan = MembershipPlanType.Monthly, Name = "Monthly", Price = 1200m, IsActive = true
});
await db.SaveChangesAsync();
var service = new MemberService(db, null!); // Registration does not use the setup service.

AddMemberViewModel ValidForm() => new()
{
    FullName = "Sample Member", Email = "sample@example.com",
    ContactNumber = "09123456789", DateOfBirth = DateTime.Today.AddYears(-20),
    SelectedPlanId = MembershipPlanType.Monthly, PaymentAmount = 1200m,
    SelectedGymGoal = GymGoal.MuscleGain, SelectedTimeSlot = "7:00 AM - 8:00 AM"
};

foreach (var invalidate in new Action<AddMemberViewModel>[]
{
    form => form.FullName = "",
    form => form.DateOfBirth = DateTime.Today.AddYears(-12),
    form => form.ContactNumber = "123",
    form => form.Email = "invalid",
    form => form.SelectedGymGoal = null,
    form => form.SelectedTimeSlot = null,
    form => form.PaymentAmount = 100m,
    form => form.SelectedTrainerId = 999
})
{
    var form = ValidForm();
    invalidate(form);
    try { await service.CreateClientAsync(form); throw new Exception("Invalid form accepted."); }
    catch (InvalidOperationException) { }
    await db.SaveChangesAsync();
    if (await db.Clients.AnyAsync() || await db.Payments.AnyAsync())
        throw new Exception("Invalid registration created a member or payment.");
}
Console.WriteLine("PASS: invalid registrations create no members or payments.");

// Fail after tracking the complete graph, then perform the later save used by
// error-page rebuilding. The failed registration must not be retried.
db.FailNextSave = true;
try { await service.CreateClientAsync(ValidForm()); throw new Exception("Expected save failure."); }
catch (DbUpdateException) { }
await db.SaveChangesAsync();
if (await db.Clients.AnyAsync() || await db.Payments.AnyAsync())
    throw new Exception("A later save persisted a rejected registration.");
Console.WriteLine("PASS: failed saves cannot leak a registration into a later save.");

// A real database constraint failure must roll back both sides of the graph.
await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_payment BEFORE INSERT ON Payments BEGIN SELECT RAISE(ABORT, 'Payment rejected'); END;");
try { await service.CreateClientAsync(ValidForm()); throw new Exception("Expected payment failure."); }
catch (DbUpdateException) { }
await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_payment;");
await db.SaveChangesAsync();
if (await db.Clients.AnyAsync() || await db.Payments.AnyAsync())
    throw new Exception("Payment failure left a partial registration.");
Console.WriteLine("PASS: payment database failure rolls back the member and payment.");

await service.CreateClientAsync(ValidForm());
if (await db.Clients.CountAsync() != 1 || await db.Payments.CountAsync() != 1 ||
    (await db.Payments.SingleAsync()).Status != PaymentStatus.Paid)
    throw new Exception("Successful registration did not save one paid membership.");
try { await service.CreateClientAsync(ValidForm()); throw new Exception("Duplicate accepted."); }
catch (InvalidOperationException) { }
await db.SaveChangesAsync();
if (await db.Clients.CountAsync() != 1 || await db.Payments.CountAsync() != 1)
    throw new Exception("Duplicate submission changed registration counts.");
Console.WriteLine("PASS: successful registration saves once; duplicate submission adds nothing.");

var walkIns = new WalkInRegistrationService(db);
var rate = new WalkInPaymentType { Name = "Day Pass", Amount = 150m };
db.WalkInPaymentTypes.Add(rate);
await db.SaveChangesAsync();
WalkInRegistrationViewModel ValidWalkIn() => new()
{
    FirstName = "Walkin", LastName = "Visitor", Email = "walkin@example.com",
    ContactNumber = "09987654321", PaymentTypeId = rate.Id, ReasonForVisit = "Day Pass"
};
var frontDesk = new FrontDeskController(service, db, null!, null!, null!,
    new EphemeralDataProtectionProvider(), null!, walkIns);
var admin = new AdminController(null!, null!, null!, null!, service, null!, db, null!, null!, walkIns);
foreach (Controller portal in new Controller[] { frontDesk, admin })
{
    portal.TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider());
    Task<IActionResult> Submit(WalkInRegistrationViewModel form) => portal == frontDesk
        ? frontDesk.WalkInRegister(form) : admin.WalkInRegister(form);
    foreach (var invalidate in new Action<WalkInRegistrationViewModel>[]
    {
        form => form.FirstName = "",
        form => form.LastName = "",
        form => form.Email = "",
        form => form.Email = "invalid",
        form => form.ContactNumber = "123",
        form => form.ReasonForVisit = "",
        form => form.PaymentTypeId = null,
        form => form.PaymentTypeId = 999
    })
    {
        var form = ValidWalkIn();
        invalidate(form);
        await Submit(form); // Service must validate even without MVC model binding.
        await db.SaveChangesAsync();
        if (await db.WalkInClients.AnyAsync() || portal.TempData.ContainsKey("RegistrationSuccess"))
            throw new Exception("Invalid walk-in produced a payment or success notice.");
        if (!portal.TempData.ContainsKey("WalkInRegistrationError"))
            throw new Exception("Invalid walk-in did not report an error.");
        portal.TempData.Clear();
    }
    portal.ModelState.AddModelError("PaymentTypeId", "Invalid submitted value.");
    await Submit(ValidWalkIn());
    portal.ModelState.Clear();
    if (await db.WalkInClients.AnyAsync()) throw new Exception("MVC errors were ignored.");
    portal.TempData.Clear();
    db.FailNextSave = true;
    await Submit(ValidWalkIn());
    await db.SaveChangesAsync();
    if (await db.WalkInClients.AnyAsync() || portal.TempData.ContainsKey("RegistrationSuccess"))
        throw new Exception("Failed walk-in save leaked a payment.");
    Console.WriteLine($"PASS: {portal.GetType().Name} rejects invalid walk-ins and failed saves without recording payments.");
}
// The configured rate, rather than a posted amount, is authoritative.
var validWalkIn = ValidWalkIn();
validWalkIn.AmountPaid = 999m;
await walkIns.RegisterAsync(validWalkIn);
if (await db.WalkInClients.CountAsync() != 1 || (await db.WalkInClients.SingleAsync()).AmountPaid != 150m)
    throw new Exception("Valid walk-in did not save the configured payment.");
try { await walkIns.RegisterAsync(ValidWalkIn()); throw new Exception("Duplicate walk-in accepted."); }
catch (InvalidOperationException) { }
await db.SaveChangesAsync();
if (await db.WalkInClients.CountAsync() != 1) throw new Exception("Duplicate walk-in created another payment.");
Console.WriteLine("PASS: completed walk-in records one configured payment; duplicate adds nothing.");

sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class RegistrationContext(DbContextOptions<GymDbContext> options) : GymDbContext(options)
{
    public bool FailNextSave { get; set; }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new DbUpdateException("Simulated registration save failure.");
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                if (property.GetDefaultValueSql() == "GETDATE()")
                    property.SetDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
