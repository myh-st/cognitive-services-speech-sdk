#!/bin/bash

# Build and Package Azure Functions for Speech Ingestion
# This script builds all functions and creates deployment packages

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
SOLUTION_FILE="BatchIngestionClient.sln"
OUTPUT_DIR="publish"
CONFIGURATION="Release"

echo -e "${GREEN}🚀 Starting build process for Speech Ingestion Functions${NC}"

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ .NET SDK not found. Please install .NET 8.0 SDK${NC}"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✅ .NET SDK version: $DOTNET_VERSION${NC}"

# Clean previous builds
echo -e "${YELLOW}🧹 Cleaning previous builds${NC}"
if [ -d "$OUTPUT_DIR" ]; then
    rm -rf "$OUTPUT_DIR"
fi
mkdir -p "$OUTPUT_DIR"

# Restore dependencies
echo -e "${YELLOW}📦 Restoring dependencies${NC}"
dotnet restore "$SOLUTION_FILE"

# Build solution
echo -e "${YELLOW}🔨 Building solution${NC}"
dotnet build "$SOLUTION_FILE" --configuration "$CONFIGURATION" --no-restore

# Run tests
echo -e "${YELLOW}🧪 Running tests${NC}"
dotnet test Tests/Tests.csproj --configuration "$CONFIGURATION" --no-build --verbosity normal

# Publish individual functions
echo -e "${YELLOW}📦 Publishing StartTranscriptionByTimer function${NC}"
dotnet publish StartTranscriptionByTimer/StartTranscriptionByTimer.csproj \
    --configuration "$CONFIGURATION" \
    --output "$OUTPUT_DIR/StartTranscriptionByTimer" \
    --no-restore

echo -e "${YELLOW}📦 Publishing FetchTranscription function${NC}"
dotnet publish FetchTranscription/FetchTranscription.csproj \
    --configuration "$CONFIGURATION" \
    --output "$OUTPUT_DIR/FetchTranscription" \
    --no-restore

echo -e "${YELLOW}📦 Publishing StartTranscriptionByServiceBus function${NC}"
dotnet publish StartTranscriptionByServiceBus/StartTranscriptionByServiceBus.csproj \
    --configuration "$CONFIGURATION" \
    --output "$OUTPUT_DIR/StartTranscriptionByServiceBus" \
    --no-restore

# Create zip packages
echo -e "${YELLOW}📦 Creating deployment packages${NC}"
cd "$OUTPUT_DIR"

# StartTranscriptionByTimer.zip
echo -e "  📄 Creating StartTranscriptionByTimer.zip"
cd StartTranscriptionByTimer
zip -r ../StartTranscriptionByTimer.zip . > /dev/null
cd ..

# FetchTranscription.zip  
echo -e "  📄 Creating FetchTranscription.zip"
cd FetchTranscription
zip -r ../FetchTranscription.zip . > /dev/null
cd ..

# StartTranscriptionByServiceBus.zip
echo -e "  📄 Creating StartTranscriptionByServiceBus.zip"
cd StartTranscriptionByServiceBus
zip -r ../StartTranscriptionByServiceBus.zip . > /dev/null
cd ..

# Verify packages
echo -e "${GREEN}✅ Deployment packages created successfully:${NC}"
ls -la *.zip | while read line; do
    echo -e "  📦 $line"
done

# Get package sizes
echo -e "${GREEN}📊 Package sizes:${NC}"
for zip_file in *.zip; do
    size=$(du -h "$zip_file" | cut -f1)
    echo -e "  📦 $zip_file: $size"
done

cd ..

echo -e "${GREEN}🎉 Build process completed successfully!${NC}"
echo -e "${GREEN}📁 Deployment packages are available in: $OUTPUT_DIR/${NC}"
echo -e "${GREEN}🚀 Ready for deployment to Azure Functions${NC}"

# Display next steps
echo -e "${YELLOW}📋 Next Steps:${NC}"
echo -e "  1. Review the deployment packages in $OUTPUT_DIR/"
echo -e "  2. Upload to Azure Functions using Azure CLI or Azure Portal"
echo -e "  3. Update Bicep template with custom URLs if needed"
echo -e "  4. Deploy infrastructure using: az deployment group create ..."
echo -e ""
echo -e "${GREEN}💡 For detailed deployment instructions, see DEPLOYMENT_GUIDE.md${NC}"
