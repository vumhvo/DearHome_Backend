using DearHome_Backend.Services.Interfaces;
using Google.Apis.Util;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DearHome_Backend.Constants;
using Microsoft.AspNetCore.Authorization;

namespace DearHome_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FirebaseStorageController : ControllerBase
    {
        private readonly IFirebaseStorageService _firebaseStorageService;

        public FirebaseStorageController(IFirebaseStorageService firebaseStorageService)
        {
            _firebaseStorageService = firebaseStorageService;
        }

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var result = await _firebaseStorageService.UploadImageAsync(file);
            return Ok(result);
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("update-image")]
        public async Task<IActionResult> UpdateFile(IFormFile file,[FromQuery] string imageUrl)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var result = await _firebaseStorageService.UpdateImageAsync(file, imageUrl);
            return Ok(result);
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpDelete("delete-image")]
        public async Task<IActionResult> DeleteFile([FromQuery] string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest("No image URL provided.");
            }

            if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            {
                return BadRequest("Invalid image URL format.");
            }

            await _firebaseStorageService.DeleteImageAsync(imageUrl);
            return Ok("Image deleted successfully.");
        }
    }
}
