using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NotesAPI.Data;
using NotesAPI.Models;
using NotesAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NotesAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly NotesDbContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;

        public AuthController(NotesDbContext context, IConfiguration config, EmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        #region Register & Login & Me
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.Username) || string.IsNullOrEmpty(user.PasswordHash))
                return BadRequest(new { message = "Missing required fields" });

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == user.Email || u.Username == user.Username);

            if (existingUser != null)
                return Conflict(new { message = "User already exists with this email or username" });

            user.PasswordHash = HashPassword(user.PasswordHash);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully" });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO login)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
            if (user == null || !VerifyPassword(login.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials");

            var token = GenerateJwt(user);
            return Ok(new { token });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (email == null)
                return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email
            });
        }
        #endregion

        #region Password Reset via Verification Code

        [AllowAnonymous]
        [HttpPost("request-reset")]
        public async Task<IActionResult> RequestReset([FromBody] ResetRequestDTO request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null) return NotFound("Email not found");

            // Generate 6-digit numeric code
            var random = new Random();
            user.ResetToken = random.Next(100000, 999999).ToString();
            user.TokenExpiry = DateTime.UtcNow.AddMinutes(15); // expires in 15 minutes

            await _context.SaveChangesAsync();

            // Send email with code
            await _emailService.SendEmailAsync(user.Email, "Password Reset Verification Code",
                $"<p>Hello {user.Username},</p>" +
                $"<p>Your password reset verification code is: <b>{user.ResetToken}</b></p>" +
                "<p>This code will expire in 15 minutes.</p>");

            return Ok(new
            {
                message = "Verification code sent to your email",
                status = "success"
            });

        }

        [AllowAnonymous]
        [HttpPost("verify-reset")]
        public async Task<IActionResult> VerifyReset([FromBody] ResetPasswordDTO request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == request.Code);

            if (user == null || user.TokenExpiry < DateTime.UtcNow)
                return BadRequest("Invalid or expired code");

            // Update password
            user.PasswordHash = HashPassword(request.NewPassword);
            user.ResetToken = null;
            user.TokenExpiry = null;

            await _context.SaveChangesAsync();
            return Ok("Password has been reset successfully");
        }

        #endregion

        #region Helpers
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private string GenerateJwt(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email,user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        #endregion
    }

    #region DTOs
    public class LoginDTO
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class ResetRequestDTO
    {
        public string Email { get; set; } = "";
    }

    public class ResetPasswordDTO
    {
        public string Code { get; set; } = "";        // Verification code
        public string NewPassword { get; set; } = ""; // New password
    }
    #endregion
}
