#!/bin/bash
set -e

echo "🚀 Starting Azure Functions locally..."

# Navigate to function directory
cd function

# Restore packages
echo "📦 Restoring NuGet packages..."
dotnet restore

# Build the project
echo "🔨 Building project..."
dotnet build

# Start Azure Functions runtime
echo "▶️ Starting Functions runtime..."
echo "Function will be available at: http://localhost:7071"
echo "Endpoints:"
echo "  GET  http://localhost:7071/api/joke"
echo "  POST http://localhost:7071/api/joke (with keywords in body)"
echo "  GET  http://localhost:7071/api/stats"
echo ""
echo "Press Ctrl+C to stop"

func start --dotnet-isolated-debug