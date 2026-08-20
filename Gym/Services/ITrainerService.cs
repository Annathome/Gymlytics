using Gym.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gym.Services
{
    public interface ITrainerService
    {
        // Accept optional target date to align with TrainerController line 36
        Task<TrainerDashboardViewModel> GetTrainerDashboardAsync(int trainerId, DateTime? date = null);

        Task<List<Session>> GetTrainerSessionsAsync(int trainerId, DateTime date);
        Task<List<Session>> GetWeeklySessionsAsync(int trainerId);

        // Return ClientViewModel list or ClientProgram list based on controller usage
        Task<IEnumerable<ClientViewModel>> GetTrainerClientsAsync(int trainerId);
        Task<ClientProgram> GetClientProgramAsync(int programId);

        // Support bool return type and direct status update
        Task<bool> UpdateSessionStatusAsync(int sessionId, SessionStatus status);
        Task<bool> BookSessionAsync(int sessionId, int clientId);
        Task<bool> CancelSessionAsync(int sessionId, int clientId);

        Task<IEnumerable<NotificationViewModel>> GetTrainerNotificationsAsync(int trainerId);
        Task<int> GetUnreadNotificationCountAsync(int trainerId);
    }
}