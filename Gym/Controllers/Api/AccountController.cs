using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;
    private readonly SetupService _setupService;

    public AuthApiController(JwtTokenService jwtTokenService  , SetupService setupService)
    {
        _jwtTokenService = jwtTokenService;
        _setupService = setupService;
   
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginViewModel model)
    {
        var (isValid, userRole, userId, username, email, phone, errorMessage) =
            await _setupService.ValidateUserAsync(model.Username, model.Password);

        if (!isValid)
        {
            return Unauthorized(new { message = errorMessage ?? "Invalid credentials." });
        }

        // Generate the JWT token string
        var token = _jwtTokenService.GenerateToken(userId, username ?? model.Username, userRole);

        return Ok(new
        {
            message = "Login successful",
            token = token, // <--- THIS WILL RETURN THE TOKEN STRING TO POSTMAN
            userId,
            username = username ?? model.Username,
            role = userRole,
            email,
            phone
        });
    }
}