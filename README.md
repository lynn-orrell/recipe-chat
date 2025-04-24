# Recipe Chat - Semantic Kernel Agent Group Chat

## Overview

The **Recipe Chat** solution is an exploration of Semantic Kernel's Agent Group Chat that aims to create and adjust food recipes using multiple Agents in a group chat. There are two user experiences that can be run: Console App and a Blazor Web App.  

The solution is divided into three main projects:

1. **RecipeChat.Console**: A console-based application for interacting with the Recipe Chat functionality.
2. **RecipeChat.Web**: A web-based application with a Blazor front-end and SignalR used to create a real-time chat experience utilizing the Recipe Chat functionality.
3. **RecipeChat.GroupChat**: The library that contains all of the SK AgentGroupChat logic including prompt templates for all of the agents as well as selection and termination strategies.

## Project Structure

### 1. **RecipeChat.Console**
- **Purpose**: Provides a console-based interface for interacting with the Recipe Chat functionality.
- **Key Files**:
  - Program.cs: Entry point for the console application.
  - Worker.cs: Handles the main chat loop and integrates with the `RecipeGroupChat` library.
  - `.env`: Configuration file for environment variables (e.g., Azure OpenAI endpoint).
- **Technologies**:
  - .NET Worker Service
  - OpenTelemetry for logging and tracing

### 2. **RecipeChat.Web**
- **Purpose**: A web-based interface for interacting with the Recipe Chat functionality.
- **Key Files**:
  - Program.cs: Configures the web application and SignalR hub.
  - RecipeChat.Web.Client/Pages/Chat.razor: The main chat page.
  - RecipeChat.Web/Hubs/ChatHub.cs: SignalR hub for real-time communication.
- **Technologies**:
  - Blazor Web App (utilizing both interactive and server-side components)
  - SignalR for real-time communication

### 3. **RecipeChat.GroupChat**
- **Purpose**: Contains the core logic for utilizing Semantic Kernel to manage and execute the group chat.
- **Key Files**:
  - RecipeGroupChat.cs: Contains all of the group chat logic.
  - `PromptTemplates/`: YAML files containing definitions for each Agent as well as the selection and termination strategies that are used to drive the group chat.
- **Technologies**:
  - Semantic Kernel Agent Framework


## Prerequisites

### 1. Tools
- [.NET 9.0 SDK](https://dotnet.microsoft.com/)
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/)
- Azure OpenAI Service instance
- Both the Console App and Web App utilize `DefaultAzureCredential` and assume you are logged in via the Azure CLI with `az login` and have the `Cognitive Services OpenAI User` RBAC role granted to you on the AOAI instance.

### 2a. (If running the console app) Environment Variables for Console App
Create a `.env` file in the RecipeChat.Console project with the following variables:
```json
AZURE_OPENAI_ENDPOINT="https://[YOUR_ENDPOINT_NAME].openai.azure.com"
AZURE_OPENAI_DEPLOYMENT_NAME="[YOUR_MODEL_DEPLOYMENT_NAME]"
OTEL_ENDPOINT="[YOUR_OTEL_ENDPOINT]"
LOG_HTTP_REQUESTS="true"
```

### 2b. (If running the web app) Environment Variables for Web App
Populate the two settings in appsettings.json in the RecipeChat.Web project:
```json
  "AZURE_OPENAI_ENDPOINT": "https://[YOUR_ENDPOINT_NAME].openai.azure.com",
  "AZURE_OPENAI_MODEL_NAME": "[YOUR_MODEL_DEPLOYMENT_NAME]"
```



## Running Each Experience
There are VS Code launch configurations for both the console and web applications. Utilize these to run your desired experience.


## Prompt to Try

Whether you are running the console app or the web app, the group chat experience is meant to be a multi-turn conversation between the user and the group chat agents.  Considering that, here is a simple prompt to try:

```
User: I'm hungry! Can you make me a recipe?

[Agent Response...]

User: I forgot to mention that I am vegan and have a gluten allergy.

[Agent Response that will call upon the VeganReviewerAgent and GlutenFreeReviewerAgent...]
```