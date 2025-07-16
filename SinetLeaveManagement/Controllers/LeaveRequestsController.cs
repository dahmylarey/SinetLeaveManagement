using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SinetLeaveManagement.Data;
using SinetLeaveManagement.Hubs;
using SinetLeaveManagement.Models;
using SinetLeaveManagement.Services;

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

        [Authorize]
        public IActionResult Index()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            var userId = currentUser?.Id;
            var requests = _context.LeaveRequests
                .Include(r => r.Employee) // Eager load the Employee navigation property
                .Where(r => User.IsInRole("ADMIN") || User.IsInRole("MANAGER") || r.EmployeeId == userId)
                .ToList();
            return View(requests);
        }

        [Authorize(Roles = "EMPLOYEE")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "EMPLOYEE")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequest model)
        {
            
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                ModelState.AddModelError("", "User is not authenticated or identity is missing.");
                return View(model);
            }

            model.EmployeeId = currentUser.Id;
            if (string.IsNullOrEmpty(model.EmployeeId))
            {
                ModelState.AddModelError("EmployeeId", "Employee ID cannot be determined.");
                return View(model);
            }

            model.Status = "Pending";
            model.CreatedAt = DateTime.Now;
            _context.LeaveRequests.Add(model);
            await _context.SaveChangesAsync();

            var managerId = await GetManagerId();
            if (string.IsNullOrEmpty(managerId) || !_context.Users.Any(u => u.Id == managerId))
            {
                ModelState.AddModelError("", "No valid manager found for notification.");
                return View(model);
            }

            var notification = new Notification
            {
                UserId = managerId,
                Message = $"New leave request from {currentUser.Email}",
                CreatedAt = DateTime.Now,
                IsRead = false
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await _notificationHub.Clients.User(notification.UserId).SendAsync("ReceiveNotification", notification.Message);
            return RedirectToAction("Index");
        }

        // GET: LeaveRequests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest == null)
            {
                return NotFound();
            }
            return View(leaveRequest);
        }

        // DELETE
        public async Task<IActionResult> Delete(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: LeaveRequests/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "EMPLOYEE, ADMIN, MANAGER, SUPERVISOR")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FindAsync(id);
            if (leaveRequest != null)
            {
                _context.LeaveRequests.Remove(leaveRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "MANAGER, ADMIN, SUPERVISOR")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request != null)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                request.Status = "Approved";
                request.ApproverId = currentUser.Id;
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId = request.EmployeeId,
                    Message = "Your leave request has been approved",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                if (string.IsNullOrEmpty(notification.UserId) || !_context.Users.Any(u => u.Id == notification.UserId))
                {
                    Console.WriteLine($"Invalid UserId for notification: {notification.UserId}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await _notificationHub.Clients.User(notification.UserId).SendAsync("ReceiveNotification", notification.Message);

                var adminEmail = "admin@example.com";
                var subject = "Leave Request Approved";
                var body = $"Leave request from {request.Employee?.FirstName} {request.Employee?.LastName} (ID: {request.Id}) has been approved on {DateTime.Now:yyyy-MM-dd HH:mm:ss}.";
                await _emailService.SendEmailAsync(adminEmail, subject, body);

                return RedirectToAction("Index");
            }
            return NotFound();
        }

        [Authorize(Roles = "MANAGER, ADMIN, SUPERVISOR")]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (request == null)
            {
                return NotFound();
            }
            return View(request);
        }

        [HttpPost]
        [Authorize(Roles = "MANAGER, ADMIN, SUPERVISOR")]
        public async Task<IActionResult> Reject(LeaveRequest model)
        {
            var request = await _context.LeaveRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == model.Id);
            if (request != null)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }

                request.Status = "Rejected";
                request.ApproverId = currentUser.Id;
                request.Comments = model.Comments;
                await _context.SaveChangesAsync();

                var notification = new Notification
                {
                    UserId = request.EmployeeId,
                    Message = $"Your leave request was rejected: {model.Comments}",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                if (string.IsNullOrEmpty(notification.UserId) || !_context.Users.Any(u => u.Id == notification.UserId))
                {
                    Console.WriteLine($"Invalid UserId for notification: {notification.UserId}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                await _notificationHub.Clients.User(notification.UserId).SendAsync("ReceiveNotification", notification.Message);

                var adminEmail = "admin@example.com";
                var subject = "Leave Request Rejected";
                var body = $"Leave request from {request.Employee?.FirstName} {request.Employee?.LastName} (ID: {request.Id}) has been rejected on {DateTime.Now:yyyy-MM-dd HH:mm:ss}. Reason: {model.Comments}";
                await _emailService.SendEmailAsync(adminEmail, subject, body);

                return RedirectToAction("Index");
            }
            return NotFound();
        }

        [Authorize]
        public IActionResult Notifications()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            var userId = currentUser?.Id;
            var notifications = _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
            return View(notifications);
        }

        [Authorize]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null && notification.UserId == (await _userManager.GetUserAsync(User))?.Id)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Notifications");
        }

        private async Task<string> GetManagerId()
        {
            var managers = await _userManager.GetUsersInRoleAsync("MANAGER");
            var manager = managers.FirstOrDefault();
            if (manager != null) return manager.Id;

            var supervisors = await _userManager.GetUsersInRoleAsync("SUPERVISOR");
            var supervisor = supervisors.FirstOrDefault();
            if (supervisor != null) return supervisor.Id;

            var admins = await _userManager.GetUsersInRoleAsync("ADMIN");
            var admin = admins.FirstOrDefault();
            return admin?.Id ?? throw new InvalidOperationException("No manager, supervisor, or admin found in the system.");
        }
    }
}





