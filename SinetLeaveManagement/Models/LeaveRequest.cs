namespace SinetLeaveManagement.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
       
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; }
        public string Status { get; set; } // Pending, Approved, Rejected
        public string Comments { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ApproverId { get; set; }
        public ApplicationUser? Approver { get; set; }
        public ApplicationUser Employee { get; set; }
    }
}