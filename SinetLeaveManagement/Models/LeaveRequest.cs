using System.ComponentModel.DataAnnotations;

namespace SinetLeaveManagement.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateTime EndDate { get; set; }
                
        [Required(ErrorMessage = "Leave type is required.")]
        public string LeaveType { get; set; } // Added to match the view

        public string Comments { get; set; } // Added to match the view

        public string? EmployeeId { get; set; }
        public string Status { get; set; }
        public string? ApproverId { get; set; }
        public DateTime CreatedAt { get; set; }
        public ApplicationUser Approver { get; set; }
        public ApplicationUser Employee { get; set; }
    }
}
