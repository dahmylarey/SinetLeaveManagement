using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SinetLeaveManagement.Models;


namespace SINETLeaveManagement.Services
{
    public static class RoleInitializer
    {
        private static readonly string[] Roles = { "Admin", "Manager", "Supervisor" };

        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // 1. Create roles if not exist
            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2. Create default admin user
            string adminEmail = "admin@sinet.com";
            string adminPassword = "Admin@123";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true,
                    JoinDate = DateTime.UtcNow,
                    //Roles = "Admin",
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}





//using Microsoft.AspNetCore.Identity;
//using SintLeaveManagement.Models;

//namespace SintLeaveManagement.Services
//{
//    public static class RoleInitializer
//    {
//        private static readonly string[] Roles = { "HR", "Manager", "Supervisor" };
//        public static async Task InitializeAsync(IServiceProvider serviceProvider)
//        {
//            var roleMgr = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
//            foreach (var role in Roles)
//                if (!await roleMgr.RoleExistsAsync(role))
//                    await roleMgr.CreateAsync(new IdentityRole(role));

//            // Optionally: create default Admin user and assign HR role
//            var userMgr = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
//            var adminEmail = "admin@sinet.com";
//            var admin = await userMgr.FindByEmailAsync(adminEmail);
//            if (admin == null)
//            {
//                admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "System Admin" };
//                await userMgr.CreateAsync(admin, "Admin@123");
//                await userMgr.AddToRoleAsync(admin, "HR");
//            }
//        }
//    }

//}
