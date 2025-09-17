# Dad Joke Function - Pulumi + Azure + OpenAI Demo

A complete Infrastructure as Code demo showcasing how Pulumi, C#, and Azure work together to deploy a serverless AI-powered joke generator.

## Overview

This demo creates a fully functional Azure Function App that generates jokes using Azure OpenAI, with everything defined as Infrastructure as Code using Pulumi:

- **🏗️ Azure Infrastructure** - Resource Group, Storage Account, Function App, OpenAI service
- **⚡ Azure Functions** - .NET 8 isolated worker serverless functions
- **🤖 Azure OpenAI** - GPT-4o-mini for joke generation
- **☁️ Modern Deployment** - Zip-based deployment via blob storage and SAS URLs
- **🔄 End-to-End Automation** - Single command deploys both infrastructure and application code

## Prerequisites

- .NET SDK 8.0 or higher
- Pulumi CLI (v3.x or later)
- An Azure subscription with access to Azure OpenAI
- Azure CLI authentication (`az login`)

## Quick Start

1. **Build the function app:**
   ```bash
   cd ../function
   ./package.sh
   ```

2. **Deploy everything:**
   ```bash
   cd ../infrastructure
   pulumi up
   ```

3. **Test your joke API:**
   ```bash
   # Get a random joke
   curl https://your-function-url.azurewebsites.net/api/joke

   # Get a joke about cats
   curl "https://your-function-url.azurewebsites.net/api/joke?keywords=cats"
   ```

4. **Clean up when done:**
   ```bash
   pulumi destroy
   ```

## Project Structure

```
c3-azure/
├── function/                    # Azure Function App
│   ├── JokeFunctions.cs         # HTTP trigger function
│   ├── Program.cs               # Function app configuration
│   ├── package.sh               # Build and package script
│   └── DadJokeFunctionApp.csproj
└── infrastructure/              # Pulumi Infrastructure as Code
    ├── Program.cs               # Azure resources definition
    ├── infrastructure.csproj    # Pulumi project
    └── README.md                # This file
```

## What Gets Created

- **Resource Group** - Container for all resources
- **Storage Account** - For Function App storage and deployment packages
- **Azure OpenAI** - Cognitive Services account with GPT-4o-mini deployment
- **App Service Plan** - Linux consumption plan for serverless functions
- **Function App** - .NET 8 isolated worker with API key authentication

## API Endpoints

- `GET /api/joke` - Returns a random dad joke
- `GET /api/joke?keywords=topic` - Returns a joke about the specified topic

## Key Technologies Demonstrated

- **Pulumi Infrastructure as Code** - Complete Azure infrastructure defined in C#
- **Azure Native Provider** - Direct Azure ARM API integration
- **Azure Functions** - Serverless .NET 8 isolated worker model
- **Azure OpenAI Integration** - GPT-4o-mini deployment and API key authentication
- **Modern Deployment** - Zip packages deployed via blob storage SAS URLs

## Architecture Highlights

This demo showcases several best practices:

- **Infrastructure as Code** - Everything reproducible and version-controlled
- **Serverless Architecture** - Pay-per-use consumption plan
- **API Key Authentication** - Simple, reliable OpenAI access
- **Single Command Deployment** - Build, package, and deploy in one step
- **Clean Resource Management** - Easy teardown with `pulumi destroy`

## Learning Resources

- [Pulumi Azure Native Documentation](https://www.pulumi.com/docs/reference/pkg/azure-native/)
- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
- [Azure OpenAI Service Documentation](https://docs.microsoft.com/en-us/azure/cognitive-services/openai/)