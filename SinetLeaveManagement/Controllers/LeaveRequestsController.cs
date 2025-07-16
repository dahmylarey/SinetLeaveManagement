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
           var requests = _context.LeaveRequests
            .Include(r => r.Employee) // Eager load the Employee navigation property
            .Where(r => User.IsInRole("ADMIN") || User.IsInRole("MANAGER") || r.EmployeeId == User.Identity.Name)
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
            Console.WriteLine($"Create attempt - Model: {Newtonsoft.Json.JsonConvert.SerializeObject(model)}, User.Identity.Name: {User.Identity.Name}");
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

            var notification = new Notification
            {
                UserId = await GetManagerId(),
                Message = $"New leave request from {currentUser.Email}",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await _notificationHub.Clients.User(notification.UserId).SendAsync("ReceiveNotification", notification.Message);
            return RedirectToAction("Index");

            //if (ModelState.IsValid)
            //{
            //    try
            //    {
                    
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine($"Error saving leave request: {ex.Message} - StackTrace: {ex.StackTrace}");
            //        ModelState.AddModelError("", "An error occurred while saving your request. Please try again or contact support.");
            //        return View(model);
            //    }
            //}
            //else
            //{
            //    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            //    {
            //        Console.WriteLine($"Validation error: {error.ErrorMessage}");
            //    }
            //    return View(model);
            //}
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


        // POST: LeaveRequests/Delete/5
        [HttpPost, ActionName("Delete")]
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
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError); // Handle null user
                }

                request.Status = "Approved";
                request.ApproverId = currentUser.Id; // Ensure ApproverId is set
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

                await _notificationHub.Clients.User(request.EmployeeId).SendAsync("ReceiveNotification", notification.Message);

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
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError); // Handle null user
                }

                request.Status = "Rejected";
                request.ApproverId = currentUser.Id; // Ensure ApproverId is set
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
            var notifications = _context.Notifications
                
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
            return managers.FirstOrDefault()?.Id ?? "default_manager_id";
        }
    }
}





