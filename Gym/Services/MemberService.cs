using Gym.Data;
using Gym.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Gym.Services
{
    public class MemberService : IMemberService
    {
        private readonly GymDbContext _context;

        public MemberService(GymDbContext context)
        {
            _context = context;
        }


        // ============================================
        // MEMBER LIST (paged + keyword search)
        // ============================================

        public async Task<MemberListViewModel> GetMemberListAsync(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Clients
                .AsNoTracking()
                .Where(c => c.Role == UserRole.Client)
                .AsQueryable();

            // 👉 Exclude soft-deleted (inactive members) from the table list
            query = query.Where(c => c.IsActiveMember);


            // ========================================
            // SEARCH (keyword across name, email, phone, membership #, id)
            // ========================================

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                query = query.Where(c =>
                    c.FirstName.Contains(searchTerm) ||
                    c.LastName.Contains(searchTerm) ||
                    (c.FirstName + " " + c.LastName)
                        .Contains(searchTerm) ||
                    c.Email.Contains(searchTerm) ||
                    c.PhoneNumber.Contains(searchTerm) ||
                    c.MembershipNumber.Contains(searchTerm) ||
                    c.UserId.ToString().Contains(searchTerm));
            }


            // ========================================
            // PLAN FILTER
            // ========================================

            if (selectedPlan.HasValue)
            {
                query = query.Where(c =>
                    c.MembershipPlan == selectedPlan.Value);
            }


            // ========================================
            // GET CLIENTS
            // ========================================

            var clients = await query
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .ToListAsync();


            // ========================================
            // TRAINER LOOKUP (for schedule display)
            // ========================================

            var trainerNames = await _context.Trainers
                .AsNoTracking()
                .ToDictionaryAsync(
                    t => t.UserId,
                    t => t.FirstName + " " + t.LastName);


            // ========================================
            // CREATE VIEW MODEL ITEMS
            // ========================================

            var members = clients
                .Select(c =>
                {
                    MemberStatus status;


                    // Expiration date
                    DateTime expirationDate =
                        c.MembershipEndDate
                        ?? CalculateExpirationDate(
                            c.MembershipStartDate,
                            c.MembershipPlan);


                    // Determine status
                    if (!c.IsActiveMember)
                    {
                        status = MemberStatus.Inactive;
                    }
                    else if (expirationDate.Date < DateTime.Today)
                    {
                        status = MemberStatus.Expired;
                    }
                    else if (expirationDate.Date <= DateTime.Today.AddDays(7))
                    {
                        status = MemberStatus.ExpiringSoon;
                    }
                    else
                    {
                        status = MemberStatus.Active;
                    }


                    // Trainer / schedule display
                    string? trainerName =
                        c.AssignedTrainerId.HasValue
                        && trainerNames.TryGetValue(
                            c.AssignedTrainerId.Value,
                            out var name)
                            ? name
                            : null;

                    string? schedule =
                        !string.IsNullOrWhiteSpace(c.PreferredDays)
                            ? $"{c.PreferredDays} · {c.PreferredTimeSlot}"
                            : null;


                    return new MemberListItemViewModel
                    {
                        Id = c.UserId,

                        MembershipNumber =
                            c.MembershipNumber,

                        FullName =
                            c.FirstName + " " + c.LastName,

                        Plan = c.MembershipPlan,

                        JoinedDate =
                            c.MembershipStartDate,

                        ExpirationDate =
                            expirationDate,

                        Status = status,

                        TrainerName = trainerName,

                        Schedule = schedule
                    };
                })
                .ToList();


            // ========================================
            // STATUS FILTER
            // ========================================

            if (selectedStatus.HasValue)
            {
                members = members
                    .Where(m => m.Status == selectedStatus.Value)
                    .ToList();
            }


            // ========================================
            // PAGING
            // ========================================

            var totalCount = members.Count;

            var pageItems = members
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();


            // ========================================
            // COUNTS
            // ========================================

            var activeCount = clients.Count(c =>
            {
                if (!c.IsActiveMember)
                    return false;

                DateTime expirationDate =
                    c.MembershipEndDate
                    ?? CalculateExpirationDate(
                        c.MembershipStartDate,
                        c.MembershipPlan);

                return expirationDate.Date >= DateTime.Today;
            });


            var pendingPaymentCount =
                await _context.Payments
                    .CountAsync(p =>
                        p.Status == PaymentStatus.Pending);


            // ========================================
            // RETURN MODEL
            // ========================================

            return new MemberListViewModel
            {
                ActiveCount = activeCount,

                PendingPaymentCount =
                    pendingPaymentCount,

                SearchTerm =
                    searchTerm ?? string.Empty,

                SelectedStatus =
                    selectedStatus,

                SelectedPlan =
                    selectedPlan,

                Paged = new PagedResult<MemberListItemViewModel>
                {
                    Items = pageItems,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount
                }
            };
        }


        // ============================================
        // CALCULATE EXPIRATION
        // ============================================

        private DateTime CalculateExpirationDate(
            DateTime startDate,
            MembershipPlanType plan)
        {
            return plan switch
            {
                MembershipPlanType.Monthly =>
                    startDate.AddMonths(1),

                MembershipPlanType.Quarterly =>
                    startDate.AddMonths(3),

                MembershipPlanType.Annual =>
                    startDate.AddYears(1),

                _ =>
                    startDate
            };
        }


        // ============================================
        // BUILD ADD MEMBER MODEL
        // ============================================

        public async Task<AddMemberViewModel>
            BuildAddMemberViewModelAsync(
                AddMemberViewModel? existing = null)
        {
            var model =
                existing ?? new AddMemberViewModel();


            // ========================================
            // TRAINERS
            // ========================================

            var trainers = await _context.Trainers
                .AsNoTracking()
                .Select(t => new SelectListItem
                {
                    Value =
                        t.UserId.ToString(),

                    Text =
                        t.FirstName + " " + t.LastName
                })
                .ToListAsync();


            trainers.Insert(
                0,
                new SelectListItem
                {
                    Value = "",
                    Text =
                        "No trainer (self-guided)"
                });

            trainers.Insert(
                1,
                new SelectListItem
                {
                    Value = "1",
                    Text =
                        "Rodolfo"
                });


            model.TrainerOptions =
                trainers;


            // ========================================
            // PLANS
            // ========================================

            model.PlanOptions =
                Enum.GetValues<MembershipPlanType>()
                .Select(p => new SelectListItem
                {
                    Value =
                        ((int)p).ToString(),

                    Text = p switch
                    {
                        MembershipPlanType.Monthly =>
                            "Monthly — ₱1,200",

                        MembershipPlanType.Quarterly =>
                            "Quarterly — ₱3,200",

                        MembershipPlanType.Annual =>
                            "Annual — ₱11,000",

                        _ =>
                            p.ToString()
                    }
                })
                .ToList();


            // ========================================
            // TIME SLOTS
            // ========================================

            model.TimeSlotOptions =
                new List<SelectListItem>
                {
                    new SelectListItem { Value = "7:00 AM - 8:00 AM",  Text = "7:00 – 8:00 AM" },
                    new SelectListItem { Value = "8:00 AM - 9:00 AM",  Text = "8:00 – 9:00 AM" },
                    new SelectListItem { Value = "9:00 AM - 10:00 AM", Text = "9:00 – 10:00 AM" },
                    new SelectListItem { Value = "10:00 AM - 11:00 AM", Text = "10:00 – 11:00 AM" },
                    new SelectListItem { Value = "11:00 AM - 12:00 AM", Text = "11:00 – 12:00 PM" },
                    new SelectListItem { Value = "12:00 PM - 1:00 PM", Text = "12:00 PM – 1:00 PM" },
                    new SelectListItem { Value = "1:00 PM - 2:00 PM", Text = "1:00 PM – 2:00 PM" },
                    new SelectListItem { Value = "2:00 PM - 3:00 PM", Text = "2:00 PM – 3:00 PM" },
                    new SelectListItem { Value = "3:00 PM - 4:00 PM", Text = "3:00 PM – 4:00 PM" },
                    new SelectListItem { Value = "8:00 PM - 9:00 PM", Text = "8:00 PM – 9:00 PM" },
                };


            // ========================================
            // CALL PURPOSE
            // ========================================

            model.PurposeOptions =
                new List<SelectListItem>
                {
                    new SelectListItem { Value = "Welcome & orientation", Text = "Welcome & orientation" },
                    new SelectListItem { Value = "Fitness Assessment", Text = "Fitness Assessment" },
                    new SelectListItem { Value = "Payment Follow-up", Text = "Payment Follow-up" }
                };


            return model;
        }


        // ============================================
        // CREATE CLIENT
        // ============================================

        public async Task CreateClientAsync(AddMemberViewModel form)
        {
            var (firstName, lastName) = form.GetSplitName();

            if (!form.SelectedPlanId.HasValue)
                throw new InvalidOperationException("Membership plan is required.");

            var plan = form.SelectedPlanId.Value;
            var expirationDate = CalculateExpirationDate(form.StartDate, plan);

            var newClient = new Client
            {
                FirstName = firstName,
                LastName = lastName,
                Email = form.Email,
                PhoneNumber = form.ContactNumber,
                Role = UserRole.Client,
                IsActive = true,
                CreatedDate = DateTime.Now,
                MembershipNumber = $"MEM-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                MembershipStartDate = form.StartDate,
                MembershipEndDate = expirationDate,
                MembershipPlan = plan,
                IsActiveMember = true,

                AssignedTrainerId = form.SelectedTrainerId,
                PreferredDays = form.SelectedDays is { Count: > 0 }
                    ? string.Join(",", form.SelectedDays) : null,
                PreferredTimeSlot = form.SelectedTimeSlot,
                Notes = form.Notes
            };

            _context.Clients.Add(newClient);
            await _context.SaveChangesAsync();
        }


        // ============================================
        // DELETE (SOFT)
        // ============================================
        public async Task DeleteClientAsync(int id)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == id && c.Role == UserRole.Client);

            if (client == null)
                return;

            client.IsActiveMember = false;
            client.IsActive = false;

            await _context.SaveChangesAsync();
        }


        // ============================================
        // REACTIVATE
        // ============================================
        public async Task ReactivateClientAsync(int id)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.UserId == id && c.Role == UserRole.Client);

            if (client == null)
                return;

            client.IsActiveMember = true;
            client.IsActive = true;

            await _context.SaveChangesAsync();
        }

    }
}