#!/bin/bash
set -e

echo "🚀 Deploying Dad Joke Function App..."

# Navigate to infrastructure directory
cd infrastructure

# Deploy infrastructure
echo "🏗️ Deploying infrastructure with Pulumi..."
export DOTNET_ROOT=/opt/homebrew/Cellar/dotnet@8/8.0.120/libexec
pulumi up --yes

# Get function app name from Pulumi outputs
FUNCTION_APP_NAME=$(pulumi stack output functionAppUrl | sed 's|https://||' | sed 's|\.azurewebsites\.net||')
RESOURCE_GROUP=$(pulumi stack output resourceGroupName)

echo "📦 Function App: $FUNCTION_APP_NAME"
echo "🏗️ Resource Group: $RESOURCE_GROUP"

# Build and publish function
cd ../function
echo "🔨 Building and publishing function..."
dotnet publish --configuration Release --output ./publish

# Deploy function code
echo "📤 Deploying function code..."
func azure functionapp publish $FUNCTION_APP_NAME --dotnet-isolated

echo "✅ Deployment complete!"
echo "🌐 Function URLs:"
FUNCTION_URL=$(pulumi stack output functionAppUrl --cwd ../infrastructure)
echo "  GET  $FUNCTION_URL/api/joke"
echo "  POST $FUNCTION_URL/api/joke"
echo "  GET  $FUNCTION_URL/api/stats"