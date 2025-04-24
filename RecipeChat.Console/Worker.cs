using System.Diagnostics;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using RecipeChat.GroupChat;
using RecipeChat.GroupChat.Models;

namespace RecipeChat;

public class Worker : BackgroundService
{
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<Worker> _logger;
    private readonly ActivitySource _activitySource;
    private readonly RecipeGroupChat _recipeGroupChat;

    public Worker(RecipeGroupChat recipeGroupChat, IHostApplicationLifetime hostApplicationLifetime, ILogger<Worker> logger, ActivitySource activitySource)
    {
        _recipeGroupChat = recipeGroupChat;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
        _activitySource = activitySource;

        _recipeGroupChat.GroupChatResponseGenerated += OnGroupChatResponseGenerated;
    }

    private void OnGroupChatResponseGenerated(object? sender, GroupChatResponseGeneratedEventArgs e)
    {
        PrettyPrint(e.ChatMessageContent, e.ChatMessageContent.Content ?? "<No Content>");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("ExecuteAsync");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("ASSISTANT: How can I help you? Type 'exit' to quit.");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("USER: ");
            string? userInput = Console.ReadLine();
            Console.ResetColor();
            if (userInput == null || userInput.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await _recipeGroupChat.AddChatMessageAsync(userInput);
            await _recipeGroupChat.StartGroupChat();
        }    

        _hostApplicationLifetime.StopApplication();    
    }

    private void PrettyPrint(ChatMessageContent chatMessageContent, string message)
    {
        Console.ForegroundColor = chatMessageContent.Role.Equals(AuthorRole.User) ? ConsoleColor.Yellow :
                                  chatMessageContent.Role.Equals(AuthorRole.Assistant) ? ConsoleColor.Gray : 
                                  ConsoleColor.White;

        Console.WriteLine($"{chatMessageContent.Role.Label.ToUpper()} [{chatMessageContent.AuthorName}]: {message}");
        Console.WriteLine();
        Console.ResetColor();
    }
}