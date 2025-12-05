using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(IExpenseService expenseService, ILogger<CategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseCategory>>> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { error = "Failed to retrieve categories", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class StatusesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<StatusesController> _logger;

    public StatusesController(IExpenseService expenseService, ILogger<StatusesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense statuses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ExpenseStatus>>> GetStatuses()
    {
        try
        {
            var statuses = await _expenseService.GetStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statuses");
            return StatusCode(500, new { error = "Failed to retrieve statuses", details = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IExpenseService expenseService, ILogger<UsersController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetUsers()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { error = "Failed to retrieve users", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific user by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        try
        {
            var user = await _expenseService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { error = $"User with ID {id} not found" });

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { error = "Failed to retrieve user", details = ex.Message });
        }
    }
}
