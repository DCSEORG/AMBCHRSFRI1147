using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;

    public bool IsChatConfigured => _chatService.IsConfigured;

    public ChatModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public void OnGet()
    {
    }
}
