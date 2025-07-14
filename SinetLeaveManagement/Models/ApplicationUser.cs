using Microsoft.AspNetCore.Identity;
using SinetLeaveManagement.Models;

namespace SinetLeaveManagement.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime JoinDate { get; set; }
        public string Role { get; set; } // Admin, Manager, Supervisor
    }
}
