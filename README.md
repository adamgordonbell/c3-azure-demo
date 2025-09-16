# Azure C# Pulumi Tutorial

A step-by-step tutorial for getting started with Pulumi and Azure using C#.

## What You'll Build

This tutorial shows you how to:
- Set up the required tools
- Create a new Azure C# Pulumi project
- Deploy Azure resources (Resource Group and Storage Account)

## Step 1: Install Prerequisites

### Install .NET 8
```bash
brew install --cask dotnet@8
```

### Install Azure CLI
```bash
brew install azure-cli
```

### Install Pulumi CLI
```bash
brew install pulumi
```

## Step 2: Authenticate with Azure

```bash
az login
```

## Step 3: Create New Pulumi Project

```bash
pulumi new azure-csharp
```

Follow the prompts to set up your project name and Azure location.

## Step 4: Preview Your Infrastructure

```bash
pulumi preview
```

This shows you what resources Pulumi will create without actually creating them.

## Step 5: Deploy Your Infrastructure

```bash
pulumi up
```

Select "yes" to deploy your Azure resources.

## Step 6: Clean Up (Optional)

When you're done, destroy the resources to avoid charges:

```bash
pulumi destroy
```

## What Gets Created

- **Resource Group**: Container for your Azure resources
- **Storage Account**: Azure storage with Standard_LRS SKU
- **Primary Storage Key**: Exported as a secret output

## Troubleshooting

If you encounter .NET runtime issues, ensure DOTNET_ROOT is set:
```bash
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet@8/8.0.120/libexec
```