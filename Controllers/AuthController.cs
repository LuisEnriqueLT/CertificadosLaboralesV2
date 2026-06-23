using CertificadosLaboralesV2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CertificadosLaboralesV2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager) : ControllerBase
    {
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            var user = await userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized("Credenciales inválidas.");

            var result = await signInManager.PasswordSignInAsync(
                user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded) return Ok();
            if (result.IsLockedOut) return BadRequest("Cuenta bloqueada temporalmente.");
            return Unauthorized("Credenciales inválidas.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return Ok();
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
