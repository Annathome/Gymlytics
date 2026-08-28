using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Gym.Models;
using Gym.Services;
using Gym.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gym.Controllers
{
    /// <summary>
    /// Admin Controller - Handles UI routes for admin portal management
    /// </summary>
    [Authorize(Roles = "Admin,MainAdmin")]
    [Route("Admin/[action]")]
    public class AdminController : Controller
    {
        private readonly IAdminService _adminService;
        private readonly SetupService _setupService;
        private readonly HashingService _hashingService;
        private readonly GmailValidationService _gmailService;
        private readonly IMemberService _memberService;
        private readonly DailyMemberReminderSender _dailyReminderSender;
        private readonly GymDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly EquipmentEvidenceService _equipmentEvidence;

        public AdminController(
            IAdminService adminService,
            SetupService setupService,
            HashingService hashingService,
            GmailValidationService gmailService,
            IMemberService memberService,
            DailyMemberReminderSender dailyReminderSender,
            GymDbContext context,
            ILogger<AdminController> logger,
            EquipmentEvidenceService equipmentEvidence)
        {
            _adminService = adminService;
            _setupService = setupService;
            _hashingService = hashingService;
            _gmailService = gmailService;
            _memberService = memberService;
            _dailyReminderSender = dailyReminderSender;
            _context = context;
            _logger = logger;
            _equipmentEvidence = equipmentEvidence;
        }

        [HttpGet("/Admin")]
        [HttpGet]
        public async Task<IActionResult> ADashboard()
        {
            try
            {
                var statistics = await _adminService.GetDashboardStatisticsAsync();
                statistics.Trainers = (await _setupService.GetActiveRegisteredTrainersAsync())
                    .Select(trainer => new AdminTrainerListItem
                    {
                        Id = trainer.StaffId,
                        FullName = trainer.FullName,
                        Email = trainer.Email,
                        IsActive = true
                    }).ToList();
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                statistics.TodayMemberRevenue = await _context.Payments.AsNoTracking()
                    .Where(payment => payment.Status == PaymentStatus.Paid && payment.PaymentDate >= today && payment.PaymentDate < tomorrow)
                    .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
                statistics.TodayWalkInRevenue = await _context.WalkInClients.AsNoTracking()
                    .Where(walkIn => walkIn.RegisteredAt >= today && walkIn.RegisteredAt < tomorrow)
                    .SumAsync(walkIn => (decimal?)walkIn.AmountPaid) ?? 0m;
                statistics.TodayMemberAttendance = await _context.MemberAttendanceLogs.AsNoTracking()
                    .CountAsync(attendance => attendance.CheckInAt >= today && attendance.CheckInAt < tomorrow);
                statistics.TodayWalkIns = await _context.WalkInClients.AsNoTracking()
                    .CountAsync(walkIn => walkIn.RegisteredAt >= today && walkIn.RegisteredAt < tomorrow);
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                var previousWeekStart = today.AddDays(-daysSinceMonday - 7);
                var deliverySuffix = $":{previousWeekStart:yyyyMMdd}";
                var sentWeeklyMemberIds = (await _context.FrontDeskEmailNotifications.AsNoTracking()
                        .Where(item => item.EmailType == "Weekly attendance summary" && item.Status == "Sent" && item.DeliveryKey.EndsWith(deliverySuffix))
                        .Select(item => item.DeliveryKey)
                        .ToListAsync())
                    .Select(key => key.Split(':'))
                    .Where(parts => parts.Length == 3 && int.TryParse(parts[1], out _))
                    .Select(parts => int.Parse(parts[1]))
                    .Distinct()
                    .Count();
                statistics.PendingWeeklyAttendanceReminders = Math.Max(0, statistics.ActiveMembers - sentWeeklyMemberIds);
                statistics.EquipmentStatus = await _context.Equipments.AsNoTracking()
                    .OrderBy(equipment => equipment.Status == "Available" ? 1 : 0)
                    .ThenBy(equipment => equipment.EquipmentName)
                    .Take(5)
                    .Select(equipment => new AdminEquipmentStatusItem
                    {
                        Name = equipment.EquipmentName,
                        Category = equipment.Category,
                        Status = equipment.Status
                    }).ToListAsync();
                return View("~/Views/AdminDashboard/ADashboard.cshtml", statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                ViewBag.ErrorMessage = "Failed to load dashboard data.";
                return View("~/Views/AdminDashboard/ADashboard.cshtml");
            }
        }

        // GET: /Admin/Members
        [HttpGet("/Admin/Members")]
        public async Task<IActionResult> Members(
            string? searchTerm,
            MemberStatus? status,
            MembershipPlanType? plan,
            int page = 1)
        {
            try
            {
                const int pageSize = 5;
                var clients = (await _adminService.GetAllClientsAsync()).ToList();
                var trainerIds = clients.Where(client => client.AssignedTrainerId.HasValue)
                    .Select(client => client.AssignedTrainerId!.Value).Distinct().ToList();
                ViewBag.TrainerNames = await _context.Trainers.AsNoTracking()
                    .Where(trainer => trainerIds.Contains(trainer.UserId))
                    .ToDictionaryAsync(trainer => trainer.UserId,
                        trainer => $"{trainer.FirstName} {trainer.LastName}");
                var clientIds = clients.Select(client => client.UserId).ToList();
                ViewBag.PaidAmounts = await _context.Payments.AsNoTracking()
                    .Where(payment => clientIds.Contains(payment.ClientId) && payment.Status == PaymentStatus.Paid)
                    .GroupBy(payment => payment.ClientId)
                    .ToDictionaryAsync(group => group.Key, group => group.Sum(payment => payment.Amount));
                var reminderSuffix = $":{DateTime.Today:yyyyMMdd}";
                ViewBag.DailyReminderMemberIds = (await _context.FrontDeskEmailNotifications.AsNoTracking()
                    .Where(item => item.Status == "Sent" && item.EmailType == "Daily member reminder" && item.DeliveryKey.EndsWith(reminderSuffix))
                    .Select(item => item.DeliveryKey).ToListAsync())
                    .Select(key => key.Split(':')).Where(parts => parts.Length == 3 && int.TryParse(parts[1], out _))
                    .Select(parts => int.Parse(parts[1])).ToHashSet();

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var term = searchTerm.Trim();
                    clients = clients.Where(client =>
                        $"{client.FirstName} {client.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        client.MembershipNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        client.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                if (status.HasValue) clients = clients.Where(client => client.Status == status.Value).ToList();
                if (plan.HasValue) clients = clients.Where(client => client.MembershipPlan == plan.Value).ToList();

                var totalCount = clients.Count;
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Clamp(page, 1, totalPages);
                ViewBag.SearchTerm = searchTerm;
                ViewBag.Status = status;
                ViewBag.Plan = plan;
                ViewBag.Page = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;
                ViewBag.ActiveCount = clients.Count(client => client.Status == MemberStatus.Active);
                ViewBag.ExpiringCount = clients.Count(client => client.Status == MemberStatus.ExpiringSoon);
                return View(
                    "~/Views/AdminDashboard/Members.cshtml",
                    clients.Skip((page - 1) * pageSize).Take(pageSize).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving members list");
                TempData["ErrorMessage"] = "Could not retrieve members list.";
                return View(
                    "~/Views/AdminDashboard/Members.cshtml",
                    new List<Client>());
            }
        }

        [HttpPost("/Admin/DeleteMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            try
            {
                await _memberService.HardDeleteClientAsync(id);
                TempData["SuccessMessage"] = "Member deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to delete member {MemberId}", id);
                TempData["ErrorMessage"] = "The member could not be deleted.";
            }

            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/Renew")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewMember(int id)
        {
            var expiration = await _memberService.RenewMembershipAsync(id);
            TempData[expiration.HasValue ? "SuccessMessage" : "ErrorMessage"] = expiration.HasValue
                ? $"Membership renewed through {expiration.Value:MMM d, yyyy}."
                : "The membership could not be renewed.";
            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/MarkInactive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMemberInactive(int id)
        {
            await _memberService.DeleteClientAsync(id);
            TempData["SuccessMessage"] = "Member marked as inactive.";
            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMember(int id)
        {
            var consumedDays = await _memberService.CancelMembershipAsync(id);
            TempData[consumedDays.HasValue ? "SuccessMessage" : "ErrorMessage"] = consumedDays.HasValue
                ? $"Membership canceled. {consumedDays.Value} attended gym day(s) consumed."
                : "The member could not be found.";
            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/Terminate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TerminateMember(int id)
        {
            await _memberService.TerminateMembershipAsync(id);
            TempData["SuccessMessage"] = "Membership terminated successfully.";
            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/Payment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordMemberPayment(int id, decimal amount)
        {
            var member = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(client => client.UserId == id && client.Role == UserRole.Client);
            if (member == null || amount <= 0)
            {
                TempData["ErrorMessage"] = "Enter a valid payment amount for an existing member.";
                return RedirectToAction(nameof(Members));
            }
            var planPrice = MembershipPlanPricing.GetPrice(member.MembershipPlan);
            var previouslyPaid = await _context.Payments.AsNoTracking()
                .Where(payment => payment.ClientId == id && payment.Status == PaymentStatus.Paid)
                .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;
            var balance = Math.Max(0m, planPrice - previouslyPaid);
            if (balance <= 0 || amount > balance)
            {
                TempData["ErrorMessage"] = balance <= 0 ? "This membership is already fully paid." : $"Payment cannot exceed ₱{balance:N2}.";
                return RedirectToAction(nameof(Members));
            }
            var paidAt = DateTime.Now;
            var duplicate = await _context.Payments.AsNoTracking().AnyAsync(payment => payment.ClientId == id && payment.Amount == amount &&
                payment.Status == PaymentStatus.Paid && payment.PaymentDate >= paidAt.Date && payment.PaymentDate < paidAt.Date.AddDays(1));
            if (duplicate)
            {
                TempData["ErrorMessage"] = "That cash payment has already been recorded for this member today.";
                return RedirectToAction(nameof(Members));
            }
            var transactionCode = $"TXN-{paidAt:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            _context.Payments.Add(new Payment { ClientId = id, Amount = amount, Status = PaymentStatus.Paid, TransactionCode = transactionCode,
                PaymentDate = paidAt, DueDate = paidAt, Notes = "Cash payment recorded by Admin." });
            await _context.SaveChangesAsync();
            var remaining = Math.Max(0m, balance - amount);
            if (!string.IsNullOrWhiteSpace(member.Email))
            {
                var (sent, error) = await _gmailService.SendMemberPaymentReceiptAsync(member.Email, $"{member.FirstName} {member.LastName}".Trim(),
                    transactionCode, amount, paidAt, MembershipPlanPricing.GetName(member.MembershipPlan), planPrice, previouslyPaid + amount, remaining);
                TempData[sent ? "SuccessMessage" : "ErrorMessage"] = sent
                    ? $"Payment recorded and receipt emailed. Remaining balance: ₱{remaining:N2}." : error;
            }
            else TempData["SuccessMessage"] = $"Payment recorded. Remaining balance: ₱{remaining:N2}.";
            return RedirectToAction(nameof(Members));
        }

        [HttpPost("/Admin/Members/DailyReminder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDailyMemberReminder(int id)
        {
            var (success, message) = await _dailyReminderSender.SendAsync(id, DateTime.Today);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
            return RedirectToAction(nameof(Members));
        }

        // GET: /Admin/Trainers
        [HttpGet("/Admin/Trainers")]
        public async Task<IActionResult> Trainers()
        {
            try
            {
                var trainers = (await _adminService.GetAllTrainersAsync())
                    .Where(trainer => trainer.IsActive)
                    .OrderBy(trainer => trainer.FirstName)
                    .ThenBy(trainer => trainer.LastName)
                    .ToList();
                var trainerIds = trainers.Select(trainer => trainer.UserId).ToList();
                var availability = await _context.TrainerAvailabilities.AsNoTracking()
                    .Where(item => trainerIds.Contains(item.TrainerId))
                    .ToDictionaryAsync(item => item.TrainerId);
                var assignedCounts = await _context.Clients.AsNoTracking()
                    .Where(client => client.AssignedTrainerId.HasValue &&
                        trainerIds.Contains(client.AssignedTrainerId.Value) &&
                        client.IsActive && client.IsActiveMember)
                    .GroupBy(client => client.AssignedTrainerId!.Value)
                    .Select(group => new { TrainerId = group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.TrainerId, item => item.Count);
                var today = DateTime.Today;
                var sessionsToday = await _context.Sessions.AsNoTracking()
                    .CountAsync(session => trainerIds.Contains(session.TrainerId) &&
                        session.StartTime >= today && session.StartTime < today.AddDays(1) &&
                        session.Status != SessionStatus.Cancelled);

                var model = new AdminTrainerManagementViewModel
                {
                    SessionsToday = sessionsToday,
                    Trainers = trainers.Select(trainer =>
                    {
                        availability.TryGetValue(trainer.UserId, out var schedule);
                        assignedCounts.TryGetValue(trainer.UserId, out var assigned);
                        return new AdminTrainerAvailabilityRow
                        {
                            TrainerId = trainer.UserId,
                            TrainerName = $"{trainer.FirstName} {trainer.LastName}",
                            AvailableDays = schedule?.AvailableDays ?? string.Empty,
                            StartTime = schedule?.StartTime,
                            EndTime = schedule?.EndTime,
                            WeeklyMemberLimit = schedule?.WeeklyMemberLimit ?? 0,
                            AssignedMembers = assigned
                        };
                    }).ToList()
                };
                return View("~/Views/AdminDashboard/AdminTrainerManagement.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trainers list");
                TempData["ErrorMessage"] = "Could not retrieve trainers list.";
                return View("~/Views/AdminDashboard/AdminTrainerManagement.cshtml",
                    new AdminTrainerManagementViewModel());
            }
        }

        // GET: /Admin/Payments
        [HttpGet("/Admin/Payments")]
        public async Task<IActionResult> Payments(string? searchTerm, PaymentStatus? status, int page = 1, bool printAll = false)
        {
            try
            {
                const int pageSize = 8;
                var allPayments = await _context.Payments.AsNoTracking()
                    .Include(payment => payment.Client)
                    .OrderByDescending(payment => payment.PaymentDate)
                    .ToListAsync();
                var walkInPayments = await _context.WalkInClients.AsNoTracking()
                    .Where(walkIn => walkIn.AmountPaid > 0)
                    .OrderByDescending(walkIn => walkIn.RegisteredAt)
                    .ToListAsync();
                var allTransactions = allPayments.Select(payment => new AdminPaymentTransaction
                {
                    Name = $"{payment.Client.FirstName} {payment.Client.LastName}".Trim(),
                    TransactionCode = payment.TransactionCode,
                    Type = "Membership",
                    Method = "Cash",
                    Amount = payment.Amount,
                    Date = payment.PaymentDate,
                    Status = payment.Status,
                    MemberId = payment.ClientId
                }).Concat(walkInPayments.Select(walkIn => new AdminPaymentTransaction
                {
                    Name = $"{walkIn.FirstName} {walkIn.LastName}".Trim(),
                    TransactionCode = $"WI-{walkIn.RegisteredAt:yyyyMMdd}-{walkIn.WalkInClientId:D5}",
                    Type = "Walk-in",
                    Method = "Cash",
                    Amount = walkIn.AmountPaid,
                    Date = walkIn.RegisteredAt,
                    Status = PaymentStatus.Paid
                })).OrderByDescending(transaction => transaction.Date).ToList();
                var filtered = FilterPaymentTransactions(allTransactions, searchTerm, status);
                var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize));
                page = Math.Clamp(page, 1, totalPages);
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var today = DateTime.Today;
                var renewalLimit = today.AddDays(7);
                var model = new AdminPaymentsViewModel
                {
                    Payments = printAll
                        ? filtered
                        : filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                    SearchTerm = searchTerm,
                    SelectedStatus = status,
                    CollectedThisMonth = allTransactions.Where(payment => payment.Status == PaymentStatus.Paid && payment.Date >= monthStart).Sum(payment => payment.Amount),
                    PendingAmount = allPayments.Where(payment => payment.Status == PaymentStatus.Pending).Sum(payment => payment.Amount),
                    OverdueAccounts = allPayments.Where(payment => payment.Status is PaymentStatus.Overdue or PaymentStatus.Unpaid).Select(payment => payment.ClientId).Distinct().Count(),
                    UpcomingRenewals = await _context.Clients.AsNoTracking().CountAsync(client =>
                        client.IsActiveMember &&
                        client.MembershipEndDate.HasValue &&
                        client.MembershipEndDate.Value >= today &&
                        client.MembershipEndDate.Value <= renewalLimit),
                    Page = page,
                    TotalPages = totalPages,
                    TotalCount = filtered.Count,
                    PlanSettings = await _context.MembershipPlanSettings.AsNoTracking().OrderBy(setting => setting.Plan).ToListAsync()
                };
                ViewBag.AutoPrint = printAll;
                return View("~/Views/AdminDashboard/AdminPayments.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments list");
                TempData["ErrorMessage"] = "Could not load payments.";
                return View("~/Views/AdminDashboard/AdminPayments.cshtml", new AdminPaymentsViewModel());
            }
        }

        [HttpPost("/Admin/Payments/Plans/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMembershipPlan(int? planId, string name, decimal price, int durationMonths)
        {
            name = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || price <= 0 || price > 10_000_000m || durationMonths is < 1 or > 120)
            {
                TempData["ErrorMessage"] = "Enter a plan name, valid price, and duration from 1 to 120 months.";
                return RedirectToAction(nameof(Payments));
            }

            MembershipPlanSetting setting;
            if (planId.HasValue)
            {
                setting = await _context.MembershipPlanSettings.FirstOrDefaultAsync(item => (int)item.Plan == planId.Value)
                    ?? throw new InvalidOperationException("Membership plan not found.");
            }
            else
            {
                var nextId = (await _context.MembershipPlanSettings.MaxAsync(item => (int?)item.Plan) ?? -1) + 1;
                setting = new MembershipPlanSetting { Plan = (MembershipPlanType)nextId };
                _context.MembershipPlanSettings.Add(setting);
            }

            if (await _context.MembershipPlanSettings.AnyAsync(item => item.Plan != setting.Plan && item.Name == name))
            {
                TempData["ErrorMessage"] = "A membership plan with that name already exists.";
                return RedirectToAction(nameof(Payments));
            }

            setting.Name = name;
            setting.Price = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
            setting.DurationMonths = durationMonths;
            setting.IsActive = true;
            setting.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            MembershipPlanPricing.Configure(setting.Plan, setting.Name, setting.Price, setting.DurationMonths, true);

            TempData["SuccessMessage"] = planId.HasValue ? "Membership plan updated." : "Membership plan added.";
            return RedirectToAction(nameof(Payments));
        }

        [HttpPost("/Admin/Payments/Plans/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMembershipPlan(int planId)
        {
            var setting = await _context.MembershipPlanSettings.FirstOrDefaultAsync(item => (int)item.Plan == planId);
            if (setting == null)
            {
                TempData["ErrorMessage"] = "Membership plan not found.";
                return RedirectToAction(nameof(Payments));
            }
            setting.IsActive = false;
            setting.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            MembershipPlanPricing.Configure(setting.Plan, setting.Name, setting.Price, setting.DurationMonths, false);
            TempData["SuccessMessage"] = "Membership plan deleted from new registrations. Existing records were preserved.";
            return RedirectToAction(nameof(Payments));
        }

        [HttpGet("/Admin/Payments/Export")]
        public async Task<IActionResult> ExportPayments(string format, string? searchTerm, PaymentStatus? status)
        {
            var payments = await _context.Payments.AsNoTracking()
                .Include(payment => payment.Client)
                .OrderByDescending(payment => payment.PaymentDate)
                .ToListAsync();
            var walkIns = await _context.WalkInClients.AsNoTracking()
                .Where(walkIn => walkIn.AmountPaid > 0)
                .OrderByDescending(walkIn => walkIn.RegisteredAt)
                .ToListAsync();
            var transactions = payments.Select(payment => new AdminPaymentTransaction
            {
                Name = $"{payment.Client.FirstName} {payment.Client.LastName}".Trim(),
                TransactionCode = payment.TransactionCode,
                Type = "Membership",
                Method = "Cash",
                Amount = payment.Amount,
                Date = payment.PaymentDate,
                Status = payment.Status,
                MemberId = payment.ClientId
            }).Concat(walkIns.Select(walkIn => new AdminPaymentTransaction
            {
                Name = $"{walkIn.FirstName} {walkIn.LastName}".Trim(),
                TransactionCode = $"WI-{walkIn.RegisteredAt:yyyyMMdd}-{walkIn.WalkInClientId:D5}",
                Type = "Walk-in",
                Method = "Cash",
                Amount = walkIn.AmountPaid,
                Date = walkIn.RegisteredAt,
                Status = PaymentStatus.Paid
            })).OrderByDescending(transaction => transaction.Date).ToList();
            transactions = FilterPaymentTransactions(transactions, searchTerm, status);

            static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
            var headers = new[] { "Member", "Transaction code", "Type", "Method", "Amount", "Date", "Status" };
            var rows = transactions.Select(payment => new[]
            {
                payment.Name,
                payment.TransactionCode,
                payment.Type,
                payment.Method,
                payment.Amount.ToString("0.00"),
                payment.Date.ToString("yyyy-MM-dd HH:mm"),
                payment.Status.ToString()
            }).ToList();
            var fileBase = $"YOKOKS-Payments-{DateTime.Today:yyyy-MM-dd}";

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = string.Join(",", headers.Select(Csv)) + "\r\n" +
                    string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileBase + ".csv");
            }

            var text = "YOKOKS PAYMENT REPORT\r\nGenerated: " + DateTime.Now.ToString("MMM dd, yyyy h:mm tt") +
                "\r\n\r\n" + string.Join(" | ", headers) + "\r\n" +
                string.Join("\r\n", rows.Select(row => string.Join(" | ", row)));
            if (string.Equals(format, "doc", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = System.Net.WebUtility.HtmlEncode(text);
                var document = $"<html><head><meta charset=\"utf-8\"><title>YOKOKS Payment Report</title></head><body><pre style=\"font:14px Arial;white-space:pre-wrap\">{encoded}</pre></body></html>";
                return File(System.Text.Encoding.UTF8.GetBytes(document), "application/msword", fileBase + ".doc");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileBase + ".txt");
        }

        private static List<Payment> FilterPayments(List<Payment> payments, string? searchTerm, PaymentStatus? status)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                payments = payments.Where(payment =>
                    $"{payment.Client.FirstName} {payment.Client.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    payment.TransactionCode.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (status.HasValue) payments = payments.Where(payment => payment.Status == status.Value).ToList();
            return payments;
        }

        private static List<AdminPaymentTransaction> FilterPaymentTransactions(
            List<AdminPaymentTransaction> transactions,
            string? searchTerm,
            PaymentStatus? status)
        {
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                transactions = transactions.Where(transaction =>
                    transaction.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.TransactionCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    transaction.Type.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (status.HasValue)
            {
                transactions = transactions.Where(transaction => transaction.Status == status.Value).ToList();
            }
            return transactions;
        }

        [HttpGet("/Admin/Attendance")]
        public async Task<IActionResult> Attendance(string? searchTerm, int page = 1)
        {
            const int pageSize = 10;
            var items = await GetAttendanceItemsAsync(searchTerm);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var model = new AdminAttendanceViewModel
            {
                Members = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                MembersCheckedIn = await _context.MemberAttendanceLogs.AsNoTracking()
                    .Where(log => log.CheckInAt >= today && log.CheckInAt < tomorrow)
                    .Select(log => log.ClientId).Distinct().CountAsync(),
                WalkInsToday = await _context.WalkInClients.AsNoTracking()
                    .CountAsync(walkIn => walkIn.RegisteredAt >= today && walkIn.RegisteredAt < tomorrow),
                CurrentlyInGym = await _context.MemberAttendanceLogs.AsNoTracking()
                    .CountAsync(log => log.CheckInAt >= today && log.CheckInAt < tomorrow && !log.CheckOutAt.HasValue),
                Page = page,
                PageSize = pageSize,
                TotalCount = items.Count
            };
            return View("~/Views/AdminDashboard/AdminAttendance.cshtml", model);
        }

        [HttpPost("/Admin/Attendance/Update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAttendance(int id, string attendanceAction)
        {
            var clientExists = await _context.Clients.AnyAsync(client => client.UserId == id && client.Role == UserRole.Client);
            if (!clientExists)
            {
                TempData["ErrorMessage"] = "Member was not found.";
                return RedirectToAction(nameof(Attendance));
            }

            var today = DateTime.Today;
            var latest = await _context.MemberAttendanceLogs
                .Where(log => log.ClientId == id && log.CheckInAt >= today && log.CheckInAt < today.AddDays(1))
                .OrderByDescending(log => log.CheckInAt).FirstOrDefaultAsync();
            if (attendanceAction == "checkin" && (latest == null || latest.CheckOutAt.HasValue))
            {
                _context.MemberAttendanceLogs.Add(new MemberAttendanceLog { ClientId = id, CheckInAt = DateTime.Now });
                TempData["SuccessMessage"] = "Member checked in successfully.";
            }
            else if (attendanceAction == "checkout" && latest != null && !latest.CheckOutAt.HasValue)
            {
                latest.CheckOutAt = DateTime.Now;
                TempData["SuccessMessage"] = "Member checked out successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "That attendance action is not currently available.";
                return RedirectToAction(nameof(Attendance));
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Attendance));
        }

        [HttpGet("/Admin/Attendance/Export")]
        public async Task<IActionResult> ExportAttendance(string format, string? searchTerm)
        {
            var members = await GetAttendanceItemsAsync(searchTerm);
            static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
            var headers = new[] { "Member", "Type", "Membership", "Check in", "Check out", "Status" };
            var rows = members.Select(member => new[]
            {
                member.FullName, "Member", member.Membership,
                member.CheckInAt?.ToString("yyyy-MM-dd h:mm tt") ?? string.Empty,
                member.CheckOutAt?.ToString("yyyy-MM-dd h:mm tt") ?? string.Empty,
                member.Status
            }).ToList();
            var fileBase = $"YOKOKS-Attendance-{DateTime.Today:yyyy-MM-dd}";
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = string.Join(",", headers.Select(Csv)) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileBase + ".csv");
            }
            var text = "YOKOKS ATTENDANCE REPORT\r\nGenerated: " + DateTime.Now.ToString("MMM dd, yyyy h:mm tt") + "\r\n\r\n" +
                string.Join(" | ", headers) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(" | ", row)));
            if (string.Equals(format, "doc", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = System.Net.WebUtility.HtmlEncode(text);
                var document = $"<html><head><meta charset=\"utf-8\"><title>YOKOKS Attendance Report</title></head><body><pre style=\"font:14px Arial;white-space:pre-wrap\">{encoded}</pre></body></html>";
                return File(System.Text.Encoding.UTF8.GetBytes(document), "application/msword", fileBase + ".doc");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileBase + ".txt");
        }

        private async Task<List<AttendanceMemberItem>> GetAttendanceItemsAsync(string? searchTerm)
        {
            var today = DateTime.Today;
            var clients = await _context.Clients.AsNoTracking().Where(client => client.Role == UserRole.Client)
                .OrderBy(client => client.LastName).ThenBy(client => client.FirstName).ToListAsync();
            var logs = await _context.MemberAttendanceLogs.AsNoTracking()
                .Where(log => log.CheckInAt >= today && log.CheckInAt < today.AddDays(1))
                .OrderByDescending(log => log.CheckInAt).ToListAsync();
            var items = clients.Select(client =>
            {
                var log = logs.FirstOrDefault(item => item.ClientId == client.UserId);
                var name = $"{client.FirstName} {client.LastName}".Trim();
                var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return new AttendanceMemberItem
                {
                    ClientId = client.UserId,
                    FullName = name,
                    Initials = (parts.Length > 1 ? $"{parts[0][0]}{parts[^1][0]}" : name[..Math.Min(2, name.Length)]).ToUpperInvariant(),
                    Membership = MembershipPlanPricing.GetName(client.MembershipPlan),
                    CheckInAt = log?.CheckInAt,
                    CheckOutAt = log?.CheckOutAt,
                    Status = log == null ? "Not checked in" : log.CheckOutAt.HasValue ? "Checked out" : "Active"
                };
            }).ToList();
            searchTerm = searchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                items = items.Where(item => item.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    item.ClientId.ToString().Contains(searchTerm) || item.Status.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    item.Membership.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return items;
        }

        // GET: /Admin/ExpiringMemberships
        [HttpGet("ExpiringMemberships")]
        public async Task<IActionResult> ExpiringMemberships(int days = 7)
        {
            try
            {
                ViewBag.Days = days;
                var memberships = await _adminService.GetExpiringMembershipsAsync(days);
                return View(memberships);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring memberships");
                TempData["ErrorMessage"] = "Could not load expiring memberships.";
                return View();
            }
        }

        // GET: /Admin/Equipment
        [HttpGet("/Admin/Equipment")]
        public async Task<IActionResult> Equipment(string? searchTerm, int page = 1)
        {
            try
            {
                const int pageSize = 8;
                searchTerm = searchTerm?.Trim();
                var query = _context.Equipments.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(searchTerm))
                    query = query.Where(item => item.EquipmentName.Contains(searchTerm) || item.Category.Contains(searchTerm) || item.Status.Contains(searchTerm));
                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Clamp(page, 1, totalPages);
                var all = await _context.Equipments.AsNoTracking().ToListAsync();
                var equipmentNames = await _context.EquipmentNameCatalog.AsNoTracking()
                    .OrderBy(item => item.Name)
                    .ToListAsync();
                var model = new AdminEquipmentViewModel
                {
                    Items = await query.OrderBy(item => item.EquipmentName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(),
                    EquipmentNames = equipmentNames,
                    SearchTerm = searchTerm ?? string.Empty,
                    TotalAssets = all.Sum(item => item.Quantity),
                    Available = all.Sum(item => item.AvailableQuantity),
                    InUse = all.Sum(item => Math.Max(0, item.Quantity - item.AvailableQuantity)),
                    Maintenance = all.Where(item => item.Status == "Needs Maintenance").Sum(item => item.Quantity),
                    OutOfService = all.Where(item => item.Status == "Out of Order").Sum(item => item.Quantity),
                    Page = page, PageSize = pageSize, TotalCount = totalCount
                };
                return View("~/Views/AdminDashboard/AdminEquipment.cshtml", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading equipment status");
                TempData["ErrorMessage"] = "Failed to load equipment list.";
                return View("~/Views/AdminDashboard/AdminEquipment.cshtml", new AdminEquipmentViewModel());
            }
        }

        [HttpPost("/Admin/Equipment/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipment(AddEquipmentViewModel form)
        {
            var allowed = new[] { "Good", "Fair", "Needs Maintenance", "Out of Order" };
            var normalizedName = form.EquipmentName?.Trim() ?? string.Empty;
            var catalogItem = await _context.EquipmentNameCatalog.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Name == normalizedName);
            if (catalogItem is null)
                ModelState.AddModelError(nameof(form.EquipmentName), "Select a name from the equipment catalog.");
            var hasIssue = form.Condition is "Fair" or "Needs Maintenance" or "Out of Order";
            if (hasIssue && (form.EvidencePhoto is null || form.EvidencePhoto.Length == 0))
                ModelState.AddModelError(nameof(form.EvidencePhoto), "Upload a photo showing the equipment issue.");
            if (form.EvidencePhoto is { Length: > 0 })
            {
                var photoError = await _equipmentEvidence.ValidateAsync(form.EvidencePhoto);
                if (photoError is not null) ModelState.AddModelError(nameof(form.EvidencePhoto), photoError);
            }
            if (!allowed.Contains(form.Condition) || !ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Complete all required equipment fields.";
                return RedirectToAction(nameof(Equipment));
            }
            string? evidencePhotoPath = null;
            if (form.EvidencePhoto is { Length: > 0 })
                evidencePhotoPath = await _equipmentEvidence.SaveAsync(form.EvidencePhoto);
            _context.Equipments.Add(new Equipment
            {
                EquipmentName = catalogItem!.Name, Category = "General", Quantity = form.Quantity,
                AvailableQuantity = form.Condition is "Good" or "Fair" ? form.Quantity : 0,
                Status = form.Condition, AddedDate = DateTime.Now,
                PurchaseDate = catalogItem.PurchaseDate,
                GymArrivalDate = catalogItem.GymArrivalDate,
                EvidencePhotoPath = evidencePhotoPath
            });
            try { await _context.SaveChangesAsync(); }
            catch { _equipmentEvidence.Delete(evidencePhotoPath); throw; }
            TempData["SuccessMessage"] = "Equipment issue report submitted successfully.";
            return RedirectToAction(nameof(Equipment));
        }

        [HttpPost("/Admin/Equipment/Names/Add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEquipmentName(AddEquipmentNameViewModel form)
        {
            var normalizedName = form.Name?.Trim() ?? string.Empty;
            if (form.GymArrivalDate is not null && form.GymArrivalDate.Value.Date > DateTime.Today)
                ModelState.AddModelError(nameof(form.GymArrivalDate), "Date brought in cannot be in the future.");
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(normalizedName))
            {
                TempData["ErrorMessage"] = "Enter a valid equipment name.";
                return RedirectToAction(nameof(Equipment));
            }

            if (await _context.EquipmentNameCatalog.AnyAsync(item => item.Name == normalizedName))
            {
                TempData["ErrorMessage"] = "That equipment name already exists.";
                return RedirectToAction(nameof(Equipment));
            }

            _context.EquipmentNameCatalog.Add(new EquipmentNameCatalog
            {
                Name = normalizedName,
                // Retained internally for compatibility with existing databases.
                PurchaseDate = form.GymArrivalDate!.Value.Date,
                GymArrivalDate = form.GymArrivalDate!.Value.Date,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Equipment name added to the Front Desk dropdown.";
            return RedirectToAction(nameof(Equipment));
        }

        [HttpGet("/Admin/Equipment/Export")]
        public async Task<IActionResult> ExportEquipment(string format, string? searchTerm)
        {
            var query = _context.Equipments.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm)) query = query.Where(item => item.EquipmentName.Contains(searchTerm) || item.Category.Contains(searchTerm) || item.Status.Contains(searchTerm));
            var items = await query.OrderBy(item => item.EquipmentName).ToListAsync();
            static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
            var headers = new[] { "Equipment", "Quantity", "Available", "Brought in", "Condition", "Last serviced", "Added" };
            var rows = items.Select(item => new[] { item.EquipmentName, item.Quantity.ToString(), item.AvailableQuantity.ToString(),
                item.GymArrivalDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                item.Status, item.LastMaintenanceDate?.ToString("yyyy-MM-dd") ?? string.Empty, item.AddedDate.ToString("yyyy-MM-dd") }).ToList();
            var fileBase = $"YOKOKS-Equipment-{DateTime.Today:yyyy-MM-dd}";
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = string.Join(",", headers.Select(Csv)) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileBase + ".csv");
            }
            var text = "YOKOKS EQUIPMENT REPORT\r\nGenerated: " + DateTime.Now.ToString("MMM dd, yyyy h:mm tt") + "\r\n\r\n" + string.Join(" | ", headers) + "\r\n" + string.Join("\r\n", rows.Select(row => string.Join(" | ", row)));
            if (string.Equals(format, "doc", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = System.Net.WebUtility.HtmlEncode(text);
                var document = $"<html><head><meta charset=\"utf-8\"><title>YOKOKS Equipment Report</title></head><body><pre style=\"font:14px Arial;white-space:pre-wrap\">{encoded}</pre></body></html>";
                return File(System.Text.Encoding.UTF8.GetBytes(document), "application/msword", fileBase + ".doc");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileBase + ".txt");
        }

        // POST: /Admin/UpdateEquipmentStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEquipmentStatus(int equipmentId, string status)
        {
            try
            {
                var success = await _adminService.UpdateEquipmentStatusAsync(equipmentId, status);
                if (success)
                {
                    TempData["SuccessMessage"] = "Equipment status updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Equipment item not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating equipment status");
                TempData["ErrorMessage"] = "An error occurred while updating status.";
            }

            return RedirectToAction(nameof(Equipment));
        }

        // GET: /Admin/UserRoles
        [HttpGet("UserRoles")]
        public async Task<IActionResult> UserRoles(int page = 1, int? confirmSuspendId = null, int? confirmDeleteId = null)
        {
            const int pageSize = 10;
            var (users, totalCount) = await _setupService.GetAllUsersAsync(page, pageSize);

            var result = new PagedResult<UserRoleViewModel>
            {
                Items = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            ViewBag.ConfirmSuspendId = confirmSuspendId;
            ViewBag.ConfirmDeleteId = confirmDeleteId;

            return View("~/Views/AdminDashboard/UserRoles.cshtml", result);
        }

        [HttpGet("UserAttendance")]
        public async Task<IActionResult> UserAttendance(int page = 1)
        {
            const int pageSize = 15;
            var (items, totalCount) = await _setupService.GetUserAttendanceAsync(page, pageSize);
            return View("~/Views/AdminDashboard/UserAttendance.cshtml", new PagedResult<UserAttendanceItem>
            {
                Items = items,
                Page = Math.Max(1, page),
                PageSize = pageSize,
                TotalCount = totalCount
            });
        }

        // GET: /Admin/UserActivity
        [HttpGet("UserActivity")]
        public async Task<IActionResult> UserActivity(int id, int page = 1)
        {
            const int pageSize = 10;

            try
            {
                // Look up the target user's display info (name, email, etc.)
                var (allUsers, _) = await _setupService.GetAllUsersAsync(1, int.MaxValue);
                var targetUser = allUsers.FirstOrDefault(u => u.Id == id);

                if (targetUser == null)
                {
                    TempData["ErrorMessage"] = "That user could not be found.";
                    return RedirectToAction(nameof(UserRoles));
                }

                // Pull activity log entries and paginate in memory
                // (GetActivityLogAsync currently only supports a flat "take" count;
                // if logs grow large, add real OFFSET/FETCH paging to SetupService instead)
                var allLogs = await _setupService.GetActivityLogAsync(id, take: 500);

                var pagedLogs = allLogs
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var result = new PagedResult<ActivityLogEntry>
                {
                    Items = pagedLogs,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = allLogs.Count
                };

                ViewBag.User = targetUser;

                return View("~/Views/AdminDashboard/UserActivity.cshtml", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activity log for user {UserId}", id);
                TempData["ErrorMessage"] = "Could not load activity log for that user.";
                return RedirectToAction(nameof(UserRoles));
            }
        }

        [HttpGet("/Admin/Members/Export")]
        public async Task<IActionResult> ExportMembers(
            string format,
            string? searchTerm,
            MemberStatus? status,
            MembershipPlanType? plan)
        {
            var clients = (await _adminService.GetAllClientsAsync()).ToList();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                clients = clients.Where(client =>
                    $"{client.FirstName} {client.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    client.MembershipNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    client.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (status.HasValue) clients = clients.Where(client => client.Status == status.Value).ToList();
            if (plan.HasValue) clients = clients.Where(client => client.MembershipPlan == plan.Value).ToList();

            static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
            var rows = clients.Select(client => new[]
            {
                client.MembershipNumber,
                $"{client.FirstName} {client.LastName}",
                client.Email,
                MembershipPlanPricing.GetName(client.MembershipPlan),
                client.PreferredDays ?? string.Empty,
                client.PreferredTimeSlot ?? string.Empty,
                client.MembershipStartDate.ToString("yyyy-MM-dd"),
                client.MembershipEndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                client.Status.ToString()
            }).ToList();
            var headers = new[] { "Member ID", "Member", "Email", "Plan", "Training days", "Training time", "Joined", "Expires", "Status" };
            var fileBase = $"YOKOKS-Members-{DateTime.Today:yyyy-MM-dd}";

            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = string.Join(",", headers.Select(Csv)) + "\r\n" +
                    string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileBase + ".csv");
            }

            var text = "YOKOKS MEMBER REPORT\r\nGenerated: " + DateTime.Now.ToString("MMM dd, yyyy h:mm tt") +
                "\r\n\r\n" + string.Join(" | ", headers) + "\r\n" +
                string.Join("\r\n", rows.Select(row => string.Join(" | ", row)));
            if (string.Equals(format, "doc", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = System.Net.WebUtility.HtmlEncode(text);
                var document = $"<html><head><meta charset=\"utf-8\"><title>YOKOKS Member Report</title></head><body><pre style=\"font:14px Arial;white-space:pre-wrap\">{encoded}</pre></body></html>";
                return File(System.Text.Encoding.UTF8.GetBytes(document), "application/msword", fileBase + ".doc");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", fileBase + ".txt");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTrainerMessage(int id, string subject, string message)
        {
            var trainer = (await _setupService.GetActiveRegisteredTrainersAsync())
                .FirstOrDefault(item => item.StaffId == id);
            if (trainer == null)
            {
                TempData["ErrorMessage"] = "The trainer is not active or could not be found.";
                return RedirectToAction(nameof(ADashboard));
            }
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Enter both a subject and a message.";
                return RedirectToAction(nameof(ADashboard));
            }

            var result = await _gmailService.SendTrainerMessageAsync(trainer.Email, trainer.FullName, subject.Trim(), message.Trim());
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Success
                ? $"Email sent to {trainer.FullName}."
                : result.ErrorMessage;
            return RedirectToAction(nameof(ADashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetUserPassword(AdminResetUserPasswordViewModel model, int page = 1)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "Enter a valid password.";
                return RedirectToAction(nameof(UserRoles), new { page });
            }

            var updated = await _setupService.UpdatePasswordHashAsync(
                model.Id,
                _hashingService.HashPassword(model.NewPassword));

            TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated
                ? "The user's password was updated successfully."
                : "The password could not be updated. Confirm that the account is active.";

            return RedirectToAction(nameof(UserRoles), new { page });
        }

        // POST: /Admin/SuspendUser
        [HttpPost("MarkStaffAbsent")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkStaffAbsent(int id, int page = 1)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("UserId")?.Value;
            if (!int.TryParse(currentUserId, out var markedByStaffId))
            {
                TempData["ErrorMessage"] = "Your administrator account could not be identified.";
                return RedirectToAction(nameof(UserRoles), new { page });
            }

            var (success, error) = await _setupService.MarkStaffAbsentAsync(id, markedByStaffId);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "The staff member was marked absent for today. Later logins will not replace this decision."
                : error;
            return RedirectToAction(nameof(UserRoles), new { page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SuspendUser(int id)
        {
            if (!TryBlockSelfAction(id, out var selfActionResult))
            {
                return selfActionResult!;
            }

            var (success, error) = await _setupService.SuspendUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account suspended." : error;
            return RedirectToAction(nameof(UserRoles));
        }

        // POST: /Admin/ReactivateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateUser(int id)
        {
            var (success, error) = await _setupService.ReactivateUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account reactivated." : error;
            return RedirectToAction(nameof(UserRoles));
        }

        // POST: /Admin/DeleteUser
        [HttpPost("DeleteUser")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!TryBlockSelfAction(id, out var selfActionResult))
            {
                return selfActionResult!;
            }

            var (success, error) = await _setupService.DeleteUserAsync(id);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account deleted." : error;
            return RedirectToAction(nameof(UserRoles));
        }

        /// <summary>
        /// Prevents an admin from performing harmful operations on their own active account
        /// </summary>
        private bool TryBlockSelfAction(int targetId, out IActionResult? result)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("UserId")?.Value;

            if (int.TryParse(currentUserId, out int myId) && myId == targetId)
            {
                TempData["ErrorMessage"] = "You cannot perform this action on your own account.";
                result = RedirectToAction(nameof(UserRoles));
                return false;
            }

            result = null;
            return true;
        }
    }
}
