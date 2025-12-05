using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Check if chat service is configured
    /// </summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return Ok(new { 
            configured = _chatService.IsConfigured,
            message = _chatService.IsConfigured 
                ? "Chat service is ready" 
                : "Chat service is not configured. Deploy with -DeployGenAI to enable."
        });
    }

    /// <summary>
    /// Send a message to the AI assistant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ChatResponse 
                { 
                    Success = false, 
                    Error = "Message cannot be empty" 
                });
            }

            var response = await _chatService.SendMessageAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ChatResponse 
            { 
                Success = false, 
                Error = $"An error occurred: {ex.Message}" 
            });
        }
    }
}
