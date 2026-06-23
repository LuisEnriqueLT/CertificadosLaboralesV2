using CertificadosLaboralesV2.Data;
using CertificadosLaboralesV2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CertificadosLaboralesV2.Services.Import
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<AppDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var roleName in new[] { "SuperAdmin", "Admin", "Empleado" })
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            if (!await context.Paises.AnyAsync())
            {
                context.Paises.AddRange(
                    new Pais { Nombre = "Colombia" },
                    new Pais { Nombre = "Chile" });
                await context.SaveChangesAsync();
            }

            var colombia = await context.Paises.FirstAsync(p => p.Nombre == "Colombia");

            const string superAdminEmail = "admin@prueba.com";
            const string superAdminPassword = "password";

            var superAdminUser = await userManager.FindByEmailAsync(superAdminEmail);
            if (superAdminUser == null)
            {
                superAdminUser = new ApplicationUser
                {
                    UserName = superAdminEmail,
                    Email = superAdminEmail,
                    EmailConfirmed = true,
                    PaisId = colombia.Id
                };

                var result = await userManager.CreateAsync(superAdminUser, superAdminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin");
            }
        }
    }
}
