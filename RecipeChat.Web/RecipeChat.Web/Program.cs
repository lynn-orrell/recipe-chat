using RecipeChat.Web.Client.Pages;
using RecipeChat.Web.Components;
using Microsoft.AspNetCore.ResponseCompression;
using RecipeChat.Web.Hubs;
using Microsoft.SemanticKernel;
using Azure.Identity;
using RecipeChat.GroupChat;

var builder = WebApplication.CreateBuilder(args);

var aoai_endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"];
var aoai_model = builder.Configuration["AZURE_OPENAI_MODEL_NAME"];

builder.Services.AddAzureOpenAIChatCompletion(aoai_model!, aoai_endpoint!, new ChainedTokenCredential(new AzureCliCredential(), new ManagedIdentityCredential()));
builder.Services.AddKernel();
builder.Services.AddTransient<RecipeGroupChat>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSignalR();

builder.Services.AddResponseCompression(options =>
{
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
});

var app = builder.Build();

app.UseResponseCompression();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RecipeChat.Web.Client._Imports).Assembly);

app.MapHub<ChatHub>("/chathub");

app.Run();
