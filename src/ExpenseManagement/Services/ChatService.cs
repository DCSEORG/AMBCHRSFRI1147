using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private readonly AzureOpenAIClient? _client;
    private readonly string? _deploymentName;

    public bool IsConfigured => _client != null && !string.IsNullOrEmpty(_deploymentName);

    public ChatService(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;

        var endpoint = configuration["OpenAI:Endpoint"];
        _deploymentName = configuration["OpenAI:DeploymentName"];

        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(_deploymentName))
        {
            try
            {
                var managedIdentityClientId = configuration["ManagedIdentityClientId"];
                Azure.Core.TokenCredential credential;

                if (!string.IsNullOrEmpty(managedIdentityClientId))
                {
                    _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                    credential = new ManagedIdentityCredential(managedIdentityClientId);
                }
                else
                {
                    _logger.LogInformation("Using DefaultAzureCredential");
                    credential = new DefaultAzureCredential();
                }

                _client = new AzureOpenAIClient(new Uri(endpoint), credential);
                _logger.LogInformation("Azure OpenAI client initialized with endpoint: {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
            }
        }
        else
        {
            _logger.LogWarning("Azure OpenAI is not configured. Set OpenAI:Endpoint and OpenAI:DeploymentName in configuration.");
        }
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request)
    {
        if (!IsConfigured)
        {
            return new ChatResponse
            {
                Success = false,
                Error = "Azure OpenAI is not configured. Deploy with the -DeployGenAI switch to enable chat functionality."
            };
        }

        try
        {
            var chatClient = _client!.GetChatClient(_deploymentName);
            
            var messages = new List<OpenAIChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add conversation history
            foreach (var msg in request.History)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new UserChatMessage(msg.Content));
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    messages.Add(new AssistantChatMessage(msg.Content));
            }

            // Add current message
            messages.Add(new UserChatMessage(request.Message));

            var options = new ChatCompletionOptions
            {
                Tools = { GetExpensesTool(), CreateExpenseTool(), GetCategoriesTool(), GetPendingExpensesTool(), ApproveExpenseTool() }
            };

            var response = await ProcessChatWithToolsAsync(chatClient, messages, options);
            
            return new ChatResponse
            {
                Success = true,
                Message = response
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Azure OpenAI");
            return new ChatResponse
            {
                Success = false,
                Error = $"Error communicating with AI: {ex.Message}"
            };
        }
    }

    private async Task<string> ProcessChatWithToolsAsync(ChatClient chatClient, List<OpenAIChatMessage> messages, ChatCompletionOptions options)
    {
        const int maxIterations = 10;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;
            var response = await chatClient.CompleteChatAsync(messages, options);
            var result = response.Value;

            if (result.FinishReason == ChatFinishReason.Stop)
            {
                return result.Content.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't generate a response.";
            }

            if (result.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(result));

                foreach (var toolCall in result.ToolCalls)
                {
                    var toolResult = await ExecuteToolAsync(toolCall);
                    messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }
            }
            else
            {
                return result.Content.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't generate a response.";
            }
        }

        return "I'm sorry, I reached the maximum number of operations. Please try a simpler request.";
    }

    private async Task<string> ExecuteToolAsync(ChatToolCall toolCall)
    {
        try
        {
            _logger.LogInformation("Executing tool: {ToolName} with arguments: {Arguments}", toolCall.FunctionName, toolCall.FunctionArguments);

            return toolCall.FunctionName switch
            {
                "get_expenses" => await HandleGetExpensesAsync(toolCall.FunctionArguments.ToString()),
                "create_expense" => await HandleCreateExpenseAsync(toolCall.FunctionArguments.ToString()),
                "get_categories" => await HandleGetCategoriesAsync(),
                "get_pending_expenses" => await HandleGetPendingExpensesAsync(),
                "approve_expense" => await HandleApproveExpenseAsync(toolCall.FunctionArguments.ToString()),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolCall.FunctionName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> HandleGetExpensesAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        int? userId = args.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetInt32() : null;
        string? searchTerm = args.TryGetProperty("searchTerm", out var searchProp) ? searchProp.GetString() : null;

        var expenses = await _expenseService.GetExpensesAsync(userId: userId, searchTerm: searchTerm);
        
        var result = expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            Amount = $"£{e.AmountDisplay:N2}",
            Date = e.ExpenseDate.ToString("dd/MM/yyyy"),
            e.StatusName,
            e.Description
        });

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> HandleCreateExpenseAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        
        var request = new CreateExpenseRequest
        {
            UserId = args.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetInt32() : 1,
            CategoryId = args.TryGetProperty("categoryId", out var catProp) ? catProp.GetInt32() : 5,
            Amount = args.TryGetProperty("amount", out var amountProp) ? amountProp.GetDecimal() : 0,
            ExpenseDate = args.TryGetProperty("date", out var dateProp) && DateTime.TryParse(dateProp.GetString(), out var date) ? date : DateTime.Today,
            Description = args.TryGetProperty("description", out var descProp) ? descProp.GetString() : null
        };

        var validation = _expenseService.ValidateExpense(request);
        if (!validation.IsValid)
        {
            return JsonSerializer.Serialize(new { success = false, error = validation.ErrorMessage });
        }

        var hourlyValidation = await _expenseService.ValidateHourlyLimitAsync(request.UserId, request.Amount);
        if (!hourlyValidation.IsValid)
        {
            return JsonSerializer.Serialize(new { success = false, error = hourlyValidation.ErrorMessage });
        }

        var expenseId = await _expenseService.CreateExpenseAsync(request);
        return JsonSerializer.Serialize(new { success = true, expenseId, message = $"Expense created with ID {expenseId}" });
    }

    private async Task<string> HandleGetCategoriesAsync()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return JsonSerializer.Serialize(categories.Select(c => new { c.CategoryId, c.CategoryName }));
    }

    private async Task<string> HandleGetPendingExpensesAsync()
    {
        var expenses = await _expenseService.GetPendingExpensesAsync();
        var result = expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            Amount = $"£{e.AmountDisplay:N2}",
            Date = e.ExpenseDate.ToString("dd/MM/yyyy"),
            e.Description
        });

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> HandleApproveExpenseAsync(string arguments)
    {
        var args = JsonSerializer.Deserialize<JsonElement>(arguments);
        
        var expenseId = args.TryGetProperty("expenseId", out var expProp) ? expProp.GetInt32() : 0;
        var reviewerId = args.TryGetProperty("reviewerId", out var revProp) ? revProp.GetInt32() : 2;

        if (expenseId == 0)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Expense ID is required" });
        }

        var success = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId);
        return JsonSerializer.Serialize(new { success, message = success ? $"Expense {expenseId} approved" : "Failed to approve expense" });
    }

    private static string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management System. You help users manage their expenses.

Available functions:
- get_expenses: Retrieve expenses from the database. Can filter by userId or searchTerm.
- create_expense: Create a new expense. Requires amount (in pounds), categoryId (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other), and optionally date and description.
- get_categories: Get list of expense categories.
- get_pending_expenses: Get expenses waiting for approval.
- approve_expense: Approve a pending expense. Requires expenseId.

Business Rules:
- Expenses cannot exceed £999
- Travel expenses cannot exceed £99
- Users cannot submit more than £1500 of expenses within 1 hour
- If description contains 'train', it's automatically categorized as Travel
- Weekend expenses are marked with (WEEKEND) in the description

When displaying lists, format them nicely with bullet points or numbered lists.
Always confirm with the user before creating or modifying data.
If asked about something outside expense management, politely redirect to expense-related topics.";
    }

    private static ChatTool GetExpensesTool()
    {
        return ChatTool.CreateFunctionTool(
            "get_expenses",
            "Retrieves expenses from the database. Can filter by user or search term.",
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""userId"": { ""type"": ""integer"", ""description"": ""Filter by user ID"" },
                    ""searchTerm"": { ""type"": ""string"", ""description"": ""Search in expense descriptions"" }
                }
            }")
        );
    }

    private static ChatTool CreateExpenseTool()
    {
        return ChatTool.CreateFunctionTool(
            "create_expense",
            "Creates a new expense record",
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""userId"": { ""type"": ""integer"", ""description"": ""User ID creating the expense"", ""default"": 1 },
                    ""categoryId"": { ""type"": ""integer"", ""description"": ""Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)"" },
                    ""amount"": { ""type"": ""number"", ""description"": ""Amount in pounds (e.g., 25.50)"" },
                    ""date"": { ""type"": ""string"", ""description"": ""Expense date in ISO format (YYYY-MM-DD)"" },
                    ""description"": { ""type"": ""string"", ""description"": ""Description of the expense"" }
                },
                ""required"": [""amount"", ""categoryId""]
            }")
        );
    }

    private static ChatTool GetCategoriesTool()
    {
        return ChatTool.CreateFunctionTool(
            "get_categories",
            "Gets the list of expense categories",
            BinaryData.FromString(@"{ ""type"": ""object"", ""properties"": {} }")
        );
    }

    private static ChatTool GetPendingExpensesTool()
    {
        return ChatTool.CreateFunctionTool(
            "get_pending_expenses",
            "Gets expenses that are pending approval",
            BinaryData.FromString(@"{ ""type"": ""object"", ""properties"": {} }")
        );
    }

    private static ChatTool ApproveExpenseTool()
    {
        return ChatTool.CreateFunctionTool(
            "approve_expense",
            "Approves a pending expense",
            BinaryData.FromString(@"{
                ""type"": ""object"",
                ""properties"": {
                    ""expenseId"": { ""type"": ""integer"", ""description"": ""ID of the expense to approve"" },
                    ""reviewerId"": { ""type"": ""integer"", ""description"": ""ID of the manager approving"", ""default"": 2 }
                },
                ""required"": [""expenseId""]
            }")
        );
    }
}
