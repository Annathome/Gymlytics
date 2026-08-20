using Gym.Models;
using System.Threading.Tasks;

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

        Task<AddMemberViewModel> BuildAddMemberViewModelAsync(AddMemberViewModel? existing = null);

        Task CreateClientAsync(AddMemberViewModel form);

        // Soft-delete: marks the member Inactive without erasing their record.
        // Pairs with ReactivateClientAsync so the action is reversible.
        Task DeleteClientAsync(int id);

        // Reverses DeleteClientAsync: marks the member Active again.
        Task ReactivateClientAsync(int id);
    }
}