using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace Gym.Models
{
    public class MembershipViewModel
    {
        [Required(ErrorMessage ="Fullname is required")]
        [StringLength(100, ErrorMessage = "Fullname must not exceed 100 character")]
        public string FullName { get; set; } = string.Empty;


        public string MembershipType { get; set; } = string.Empty;


        [Key]
        public int MembershipId { get; set; }

        public string? goalDescription {  get; set;}


        [DataType(DataType.DateTime)]
        public DateTime? Schedule {  get; set; }

    }

    public enum goalDescription
    {
       weightLoss = 0, cardio = 1, muscleEnhancement = 2,

    }



}
