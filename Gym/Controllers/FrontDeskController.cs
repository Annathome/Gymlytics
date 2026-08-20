using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gym.Controllers
{
    public class FrontDeskController : Controller
    {
        private readonly IMemberService _memberService;

        public FrontDeskController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        // ============================================
        // MEMBER LIST  (also handles "openDrawer=true")
        // ============================================
        [HttpGet]
        public async Task<IActionResult> Member(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page = 1,
            int pageSize = 10,
            bool openDrawer = false)
        {
            var viewModel = await BuildMembersPageViewModelAsync(
                searchTerm, selectedStatus, selectedPlan, page, pageSize, openDrawer);

            return View(viewModel);
        }

        // ============================================
        // ADD MEMBER - SHOW FORM
        // ============================================
        [HttpGet]
        public async Task<IActionResult> AddMember()
        {
            var viewModel = await BuildMembersPageViewModelAsync(
                searchTerm: null,
                selectedStatus: null,
                selectedPlan: null,
                page: 1,
                pageSize: 10,
                openDrawer: false);

            return View(viewModel);
        }

        // ============================================
        // SHARED HELPER
        // ============================================
        private async Task<MembersPageViewModel> BuildMembersPageViewModelAsync(
            string? searchTerm,
            MemberStatus? selectedStatus,
            MembershipPlanType? selectedPlan,
            int page,
            int pageSize,
            bool openDrawer)
        {
            var listModel = await _memberService.GetMemberListAsync(
                searchTerm, selectedStatus, selectedPlan, page, pageSize);

            var addModel = await _memberService.BuildAddMemberViewModelAsync();

            return new MembersPageViewModel
            {
                ListViewModel = listModel,
                AddMemberViewModel = addModel,
                OpenDrawer = openDrawer
            };
        }

        // ============================================
        // ADD MEMBER - SAVE
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember([Bind(Prefix = "AddMemberViewModel")] AddMemberViewModel form)
        {
            if (!ModelState.IsValid)
            {
                form = await _memberService.BuildAddMemberViewModelAsync(form);
                var listModel = await _memberService.GetMemberListAsync(null, null, null, 1, 10);

                var viewModel = new MembersPageViewModel
                {
                    ListViewModel = listModel,
                    AddMemberViewModel = form
                };

                return View("AddMember", viewModel);
            }

            await _memberService.CreateClientAsync(form);

            TempData["SuccessMessage"] = $"Member '{form.FullName}' registered successfully!";

            return RedirectToAction(nameof(Member));
        }

        // ============================================
        // DELETE MEMBER (soft delete -> Inactive)
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMember(int id)
        {
            await _memberService.DeleteClientAsync(id);

            TempData["SuccessMessage"] = "Member removed from the active roster successfully.";

            return RedirectToAction(nameof(Member));
        }

        // ============================================
        // REACTIVATE MEMBER
        // ============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateMember(int id)
        {
            await _memberService.ReactivateClientAsync(id);

            TempData["SuccessMessage"] = "Member reactivated successfully!";

            return RedirectToAction(nameof(Member));
        }
    }
}