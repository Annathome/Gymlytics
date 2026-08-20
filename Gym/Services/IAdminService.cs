using Gym.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gym.Services
{
    public interface IAdminService
    {
        Task<DashboardStatistics> GetDashboardStatisticsAsync();
        Task<List<RecentPayment>> GetRecentPaymentsAsync(int count = 10);
        Task<List<ExpiringMembership>> GetExpiringMembershipsAsync(int daysWindow = 7);
        Task<List<WeeklyAttendanceData>> GetWeeklyAttendanceAsync();
        Task<List<Equipment>> GetEquipmentStatusAsync();
        Task<List<Trainer>> GetAllTrainersAsync();
        Task<List<Client>> GetAllClientsAsync();
        Task<List<Payment>> GetPaymentsAsync(PaymentStatus? status = null);
        Task<bool> UpdateEquipmentStatusAsync(int equipmentId, string status);
        Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate);
        
    }
}