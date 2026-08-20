using Gym.Models;
using Gym.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gym.Services
{
    public class AdminService : IAdminService
    {
        private readonly IRepository<Client> _clientRepository;
        private readonly IRepository<Payment> _paymentRepository;
        private readonly IRepository<Session> _sessionRepository;
        private readonly IRepository<Equipment> _equipmentRepository;
        private readonly IRepository<SessionAttendee> _attendeeRepository;

        public AdminService(
            IRepository<Client> clientRepository,
            IRepository<Payment> paymentRepository,
            IRepository<Session> sessionRepository,
            IRepository<Equipment> equipmentRepository,
            IRepository<SessionAttendee> attendeeRepository)
        {
            _clientRepository = clientRepository;
            _paymentRepository = paymentRepository;
            _sessionRepository = sessionRepository;
            _equipmentRepository = equipmentRepository;
            _attendeeRepository = attendeeRepository;
        }

        public async Task<DashboardStatistics> GetDashboardStatisticsAsync()
        {
            var allClients = (await _clientRepository.GetAllAsync()).ToList();
            var activeClients = allClients.Where(c => c.IsActiveMember).ToList();
            var today = DateTime.Now.Date;

            var todayAttendees = (await _attendeeRepository
                .GetAllAsync(a => a.Session.StartTime.Date == today && a.IsAttended)).ToList();

            var pendingPayments = (await _paymentRepository
                .GetAllAsync(p => p.Status == PaymentStatus.Pending ||
                           p.Status == PaymentStatus.Overdue)).ToList();

            var equipment = (await _equipmentRepository.GetAllAsync()).ToList();
            var equipmentIssues = equipment
                .Count(e => e.Status != "Available" || e.AvailableQuantity == 0);

            var expiringMembershipsList = await GetExpiringMembershipsAsync();

            var weeklyAttendance = await GetWeeklyAttendanceAsync();

            return new DashboardStatistics
            {
                TotalMembers = allClients.Count,
                ActiveMembers = activeClients.Count,
                TodayAttendance = todayAttendees.Count,
                PendingPayments = pendingPayments.Sum(p => p.Amount),
                EquipmentIssues = equipmentIssues,
                ExpiringMembershipsCount = expiringMembershipsList.Count, // Assuming integer count property
                ExpiringMemberships = expiringMembershipsList,
                WeeklyAttendance = weeklyAttendance,
                RecentPayments = await GetRecentPaymentsAsync(5)
            };
        }

        public async Task<List<RecentPayment>> GetRecentPaymentsAsync(int count = 10)
        {
            var payments = await _paymentRepository.GetAllAsync();

            return payments
                .OrderByDescending(p => p.PaymentDate)
                .Take(count)
                .Select(p => new RecentPayment
                {
                    PaymentId = p.PaymentId,
                    ClientName = p.Client.FirstName + " " + p.Client.LastName,
                    TransactionCode = p.TransactionCode,
                    Amount = p.Amount,
                    Status = p.Status,
                    PaymentDate = p.PaymentDate
                })
                .ToList();
        }

        public async Task<List<ExpiringMembership>> GetExpiringMembershipsAsync(int daysWindow = 7)
        {
            var today = DateTime.Now.Date;
            var windowEnd = today.AddDays(daysWindow);

            var clients = await _clientRepository.GetAllAsync();

            return clients
                .Where(c => c.IsActiveMember && c.MembershipEndDate.HasValue &&
                       c.MembershipEndDate.Value >= today &&
                       c.MembershipEndDate.Value <= windowEnd)
                .Select(c => new ExpiringMembership
                {
                    ClientId = c.UserId,
                    ClientName = c.FirstName + " " + c.LastName,
                    ExpirationDate = c.MembershipEndDate.Value,
                    DaysRemaining = (int)(c.MembershipEndDate.Value - today).TotalDays
                })
                .OrderBy(e => e.ExpirationDate)
                .ToList();
        }

        public async Task<List<WeeklyAttendanceData>> GetWeeklyAttendanceAsync()
        {
            var today = DateTime.Now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var attendees = await _attendeeRepository.GetAllAsync();

            var days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var result = new List<WeeklyAttendanceData>();

            for (int i = 0; i < 7; i++)
            {
                var dayDate = weekStart.AddDays(i);
                var count = attendees.Count(a => a.Session.StartTime.Date == dayDate && a.IsAttended);
                result.Add(new WeeklyAttendanceData { Day = days[i], Count = count });
            }

            return result;
        }

        public async Task<List<Equipment>> GetEquipmentStatusAsync()
        {
            return (await _equipmentRepository.GetAllAsync()).ToList();
        }

        public async Task<List<Trainer>> GetAllTrainersAsync()
        {
            return new List<Trainer>();
        }

        public async Task<List<Client>> GetAllClientsAsync()
        {
            return (await _clientRepository.GetAllAsync()).ToList();
        }

        public async Task<List<Payment>> GetPaymentsAsync(PaymentStatus? status = null)
        {
            if (status.HasValue)
                return (await _paymentRepository.GetAllAsync(p => p.Status == status.Value)).ToList();

            return (await _paymentRepository.GetAllAsync()).ToList();
        }

        public async Task<bool> UpdateEquipmentStatusAsync(int equipmentId, string status)
        {
            var equipment = await _equipmentRepository.GetByIdAsync(equipmentId);
            if (equipment == null) return false;

            equipment.Status = status;
            await _equipmentRepository.UpdateAsync(equipment);
            return true;
        }

        public async Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate)
        {
            var payments = await _paymentRepository.GetAllAsync(
                p => p.Status == PaymentStatus.Paid &&
                     p.PaymentDate >= startDate &&
                     p.PaymentDate <= endDate);

            return payments.Sum(p => p.Amount);
        }
    }
}