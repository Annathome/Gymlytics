using Gym.Models;

namespace Gym.Services
{
    public interface IMemberService
    {
        Task<MemberListViewModel> GetMemberListAsync(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page = 1,
            int pageSize = 10);

        Task<AddMemberViewModel> BuildAddMemberViewModelAsync(
            AddMemberViewModel? existing = null);

        Task CreateClientAsync(AddMemberViewModel form);

        Task DeleteClientAsync(int id);

        Task ReactivateClientAsync(int id);

        Task<DateTime?> RenewMembershipAsync(int id);

        Task<bool> UpdateAssignmentAndFollowUpAsync(
            int id,
            int? assignedTrainerId,
            DateTime? followUpDate,
            string? followUpPurpose,
            IEnumerable<string>? preferredDays,
            string? preferredTimeSlot);

        Task<int?> CancelMembershipAsync(int id);

        Task TerminateMembershipAsync(int id);

        Task HardDeleteClientAsync(int id);
    }
}
