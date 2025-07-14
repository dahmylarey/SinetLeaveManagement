using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NETCore.MailKit.Core;
using SinetLeaveManagement.Data;
using SinetLeaveManagement.Hubs;
using SinetLeaveManagement.Models;

namespace SinetLeaveManagement.Controllers
{
    [Authorize]
    public class LeaveRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public LeaveRequestsController(ApplicationDbContext context, IHubContext<NotificationHub> notificationHub, UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _context = context;
            _notificationHub = notificationHub;
            _userManager = userManager;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            var requests = _context.LeaveRequests
                .Where(r => User.IsInRole("Admin") || User.IsInRole("Manager") || r.EmployeeId == User.Identity.Name)
                .ToList();
            return View(requests);
        }

        [Authorize(Roles = "Employee")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Employee")]
        public async Task<IActionResult> Create(LeaveRequest model)
        {
            if (ModelState.IsValid)
            {
                model.EmployeeId = User.Identity.Name;
                model.Status = "Pending";
                model.CreatedAt = DateTime.Now;
                _context.LeaveRequests.Add(model);
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId = "manager_id", // Replace with actual logic to get manager
                    Message = $"New leave request from {User.Identity.Name}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await _notificationHub.Clients.User("manager_id").SendAsync("ReceiveNotification", notification.Message);
                return RedirectToAction("Index");
            }
            return View(model);
        }

        [Authorize(Roles = "Manager, Supervisor")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.LeaveRequests.FindAsync(id);
            if (request != null)
            {
                request.Status = "Approved";
                request.ApproverId = User.Identity.Name;
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId = request.EmployeeId,
                    Message = "Your leave request has been approved",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                //notify the employee via SignalR
                await _notificationHub.Clients.User(request.EmployeeId).SendAsync("ReceiveNotification", notification.Message);

                // Send email to Admin
                var adminEmail = "admin@example.com"; // Replace with actual admin email
                var subject = "Leave Request Approved";
                var body = $"Leave request from {request.Employee.FirstName} {request.Employee.LastName} (ID: {request.Id}) has been approved on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.";
                await _emailService.SendAsync(adminEmail, subject, body);

                return RedirectToAction("Index");
            }
            return NotFound();
        }

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

                //notify the employee via SignalR
                await _notificationHub.Clients.User(request.EmployeeId).SendAsync("ReceiveNotification", notification.Message);

                // Send email to Admin
                var adminEmail = "admin@example.com"; // Replace with actual admin email
                var subject = "Leave Request Rejected";
                var body = $"Leave request from {request.Employee.FirstName} {request.Employee.LastName} (ID: {request.Id}) has been rejected on {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Reason: {model.Comments}";
                await _emailService.SendAsync(adminEmail, subject, body);

                return RedirectToAction("Index");
            }
            return NotFound();
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
