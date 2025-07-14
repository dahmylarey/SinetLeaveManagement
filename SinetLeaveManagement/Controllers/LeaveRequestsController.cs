using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SinetLeaveManagement.Data;
using SinetLeaveManagement.Hubs;
using SinetLeaveManagement.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SinetLeaveManagement.Controllers
{
    [Authorize]
    public class LeaveRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeaveRequestsController(
            ApplicationDbContext context,
            IHubContext<NotificationHub> notificationHub,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _notificationHub = notificationHub;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            IQueryable<LeaveRequest> requests;

            if (roles.Contains("Admin") || roles.Contains("Manager") || roles.Contains("Supervisor"))
            {
                requests = _context.LeaveRequests;
            }
            else
            {
                requests = _context.LeaveRequests.Where(r => r.EmployeeId == user.Id);
            }

            return View(await requests.ToListAsync());
        }

        [Authorize(Roles = "Employee")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Employee")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequest model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                model.EmployeeId = user.Id;
                model.Status = "Pending";
                model.CreatedAt = DateTime.Now;

                _context.LeaveRequests.Add(model);

                // Example: notify all managers and supervisors
                var managerIds = _userManager.Users
                    .Where(u => _context.UserRoles
                        .Any(r => r.UserId == u.Id &&
                                 (r.RoleId == _context.Roles.FirstOrDefault(role => role.Name == "Manager").Id
                                  || r.RoleId == _context.Roles.FirstOrDefault(role => role.Name == "Supervisor").Id)))
                    .Select(u => u.Id)
                    .ToList();

                foreach (var managerId in managerIds)
                {
                    var notification = new Notification
                    {
                        UserId = managerId,
                        Message = $"New leave request from {user.UserName}",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();

                foreach (var managerId in managerIds)
                {
                    await _notificationHub.Clients.User(managerId).SendAsync("ReceiveNotification", $"New leave request from {user.UserName}");
                }

                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        //Approve leave request
        [Authorize(Roles = "Manager, Supervisor")]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }
            return View(request);
        }

        [HttpPost]
        [Authorize(Roles = "Manager, Supervisor")]
        public async Task<IActionResult> Reject(LeaveRequest model)
        {
            var request = await _context.LeaveRequests.FindAsync(model.Id);
            if (request != null)
            {
                request.Status = "Rejected";
                request.ApproverId = User.Identity.Name;
                request.Comments = model.Comments;
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId = request.EmployeeId,
                    Message = $"Your leave request was rejected: {model.Comments}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await _notificationHub.Clients.User(request.EmployeeId).SendAsync("ReceiveNotification", notification.Message);
            }
            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult Notifications()
        {
            var notifications = _context.Notifications
                .Where(n => n.UserId == User.Identity.Name)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
            return View(notifications);
        }

        [Authorize]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null && notification.UserId == User.Identity.Name)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Notifications");
        }
    }
}
