using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace RecipeChat.Plugins;

public class IngredientsPlugin
{
    private readonly ILogger<IngredientsPlugin> _logger;

    public IngredientsPlugin(ILogger<IngredientsPlugin> logger)
    {
        _logger = logger;    
    }

    [KernelFunction]
    [Description("Returns a list of available ingredients")]
    public List<string> GetAvailableIngredients()
    {
        _logger.LogInformation("GetAvailableIngredients called");
        return new List<string> { "Flour", "Sugar", "Eggs", "Butter", "Milk" };
    }
}
