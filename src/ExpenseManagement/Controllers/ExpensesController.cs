using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Expense>>> GetExpenses(
        [FromQuery] int? userId = null,
        [FromQuery] int? statusId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] string? searchTerm = null)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesAsync(userId, statusId, categoryId, searchTerm);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses");
            return StatusCode(500, new { error = "Failed to retrieve expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetExpense(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
                return NotFound(new { error = $"Expense with ID {id} not found" });
            
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to retrieve expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expenses pending approval
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<Expense>>> GetPendingExpenses([FromQuery] string? searchTerm = null)
    {
        try
        {
            var expenses = await _expenseService.GetPendingExpensesAsync(searchTerm);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending expenses");
            return StatusCode(500, new { error = "Failed to retrieve pending expenses", details = ex.Message });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        try
        {
            // Validate business rules
            var validation = _expenseService.ValidateExpense(request);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.ErrorMessage });
            }

            // Check hourly limit
            var hourlyValidation = await _expenseService.ValidateHourlyLimitAsync(request.UserId, request.Amount);
            if (!hourlyValidation.IsValid)
            {
                return BadRequest(new { error = hourlyValidation.ErrorMessage });
            }

            var expenseId = await _expenseService.CreateExpenseAsync(request);
            var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
            
            return CreatedAtAction(nameof(GetExpense), new { id = expenseId }, expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Failed to create expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            // Validate business rules (convert to CreateExpenseRequest for validation)
            var createRequest = new CreateExpenseRequest
            {
                CategoryId = request.CategoryId,
                Amount = request.Amount
            };
            var validation = _expenseService.ValidateExpense(createRequest);
            if (!validation.IsValid)
            {
                return BadRequest(new { error = validation.ErrorMessage });
            }

            var success = await _expenseService.UpdateExpenseAsync(id, request);
            if (!success)
                return NotFound(new { error = $"Expense with ID {id} not found or could not be updated" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to update expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult> SubmitExpense(int id)
    {
        try
        {
            var success = await _expenseService.SubmitExpenseAsync(id);
            if (!success)
                return NotFound(new { error = $"Expense with ID {id} not found or could not be submitted" });

            return Ok(new { message = "Expense submitted for approval" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to submit expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> ApproveExpense(int id, [FromBody] ApproveRejectRequest request)
    {
        try
        {
            var success = await _expenseService.ApproveExpenseAsync(id, request.ReviewerId);
            if (!success)
                return NotFound(new { error = $"Expense with ID {id} not found or could not be approved" });

            return Ok(new { message = "Expense approved" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to approve expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> RejectExpense(int id, [FromBody] ApproveRejectRequest request)
    {
        try
        {
            var success = await _expenseService.RejectExpenseAsync(id, request.ReviewerId);
            if (!success)
                return NotFound(new { error = $"Expense with ID {id} not found or could not be rejected" });

            return Ok(new { message = "Expense rejected" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to reject expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Delete a draft expense
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteExpense(int id)
    {
        try
        {
            var success = await _expenseService.DeleteExpenseAsync(id);
            if (!success)
                return NotFound(new { error = $"Expense with ID {id} not found or is not a draft" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to delete expense", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expense summary by status
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetExpenseSummary([FromQuery] int? userId = null)
    {
        try
        {
            var summary = await _expenseService.GetExpenseSummaryAsync(userId);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense summary");
            return StatusCode(500, new { error = "Failed to retrieve expense summary", details = ex.Message });
        }
    }

    /// <summary>
    /// Get expense summary by category
    /// </summary>
    [HttpGet("by-category")]
    public async Task<ActionResult<List<CategorySummary>>> GetExpensesByCategory([FromQuery] int? userId = null)
    {
        try
        {
            var summary = await _expenseService.GetExpensesByCategoryAsync(userId);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expenses by category");
            return StatusCode(500, new { error = "Failed to retrieve category summary", details = ex.Message });
        }
    }
}
