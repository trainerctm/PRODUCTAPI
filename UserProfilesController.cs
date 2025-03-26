using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ProductApi.Data;
using ProductApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ProductApi.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfilesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserProfilesController(AppDbContext context)
        {
            _context = context;
        }

        // GET api/userprofiles/me
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var githubId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.GitHubId == githubId);
            if (profile == null)
                return NotFound();
            return Ok(profile);
        }

        // PUT api/userprofiles/me
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile(UserProfile updatedProfile)
        {
            var githubId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.GitHubId == githubId);
            if (profile == null)
                return NotFound();

            // Update only user-editable fields
            profile.FirstName = updatedProfile.FirstName;
            profile.LastName = updatedProfile.LastName;
            profile.Address = updatedProfile.Address;
            profile.Email = updatedProfile.Email;
            profile.PhoneNumber = updatedProfile.PhoneNumber;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET api/userprofiles (for admins to list all profiles)
        [HttpGet]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> GetAllProfiles()
        {
            var profiles = await _context.UserProfiles.ToListAsync();
            return Ok(profiles);
        }

        // PUT api/userprofiles/{id}/role – for admins to update a user's role
        [HttpPut("{id}/role")]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] string role)
        {
            var profile = await _context.UserProfiles.FindAsync(id);
            if (profile == null)
                return NotFound();

            profile.Role = role;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
