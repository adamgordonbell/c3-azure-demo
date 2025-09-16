# CLAUDE.md

This is a **demo project** showcasing how Pulumi, C#, and Azure work together seamlessly.

## What This Demonstrates

- **🏗️ Pulumi Infrastructure as Code**: Complete Azure infrastructure defined in C#
- **⚡ Azure Functions**: Serverless .NET 8 isolated worker functions
- **☁️ Modern Deployment**: Zip-based deployment via blob storage and SAS URLs
- **🔄 End-to-End Automation**: Single command deploys both infrastructure and application code

## Key Technologies

- **Pulumi** with Azure Native provider
- **C# .NET 8** for both infrastructure and function code
- **Azure Functions** (Linux consumption plan)
- **Azure Table Storage** for persistence
- **Pure Infrastructure as Code** approach

## Getting Started

Follow the complete walkthrough in `TUTORIAL.md` which demonstrates:

1. Setting up the development environment
2. Building the .NET Azure Function locally
3. Defining Azure infrastructure with Pulumi in C#
4. Deploying everything with a single `pulumi up` command
5. Testing the live endpoints

The tutorial shows how all these technologies integrate smoothly without manual deployment steps or Azure CLI dependency for function publishing.

## Result

A working serverless API with two endpoints that demonstrates persistent storage, error handling, and modern Azure development practices - all managed through Infrastructure as Code.