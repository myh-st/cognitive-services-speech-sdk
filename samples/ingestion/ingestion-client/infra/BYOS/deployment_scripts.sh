#!/bin/bash

# Azure Speech Batch Transcription Deployment Script
# This script provides easy deployment options for both BYOS and regular scenarios

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check prerequisites
check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check if user is logged in
    if ! az account show &> /dev/null; then
        print_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    print_success "Prerequisites check passed"
}

# Function to validate parameters
validate_parameters() {
    local scenario=$1
    
    if [ "$scenario" = "byos" ]; then
        if [ -z "$EXISTING_STORAGE_NAME" ] || [ -z "$EXISTING_STORAGE_RG" ]; then
            print_error "For BYOS deployment, EXISTING_STORAGE_NAME and EXISTING_STORAGE_RG are required"
            exit 1
        fi
        
        # Check if existing storage account exists
        if ! az storage account show --name "$EXISTING_STORAGE_NAME" --resource-group "$EXISTING_STORAGE_RG" &> /dev/null; then
            print_error "Storage account '$EXISTING_STORAGE_NAME' not found in resource group '$EXISTING_STORAGE_RG'"
            exit 1
        fi
        
        print_success "Existing storage account validated"
    fi
    
    # Validate required parameters
    if [ -z "$SPEECH_KEY" ] || [ -z "$SPEECH_REGION" ]; then
        print_error "SPEECH_KEY and SPEECH_REGION are required"
        exit 1
    fi
}

# Function to deploy BYOS scenario
deploy_byos() {
    print_info "Deploying BYOS scenario..."
    print_info "Using existing storage: $EXISTING_STORAGE_NAME in $EXISTING_STORAGE_RG"
    
    # Create deployment
    az deployment group create \
        --resource-group "$TARGET_RG" \
        --template-file template.json \
        --parameters \
            StorageAccount="$EXISTING_STORAGE_NAME" \
            UseExistingStorageAccount=true \
            ExistingStorageResourceGroup="$EXISTING_STORAGE_RG" \
            IsByosEnabledDeployment=true \
            AzureSpeechServicesKey="$SPEECH_KEY" \
            AzureSpeechServicesRegion="$SPEECH_REGION" \
            Locale="$LOCALE" \
            DeploymentId="$DEPLOYMENT_ID" \
        --name "speech-transcription-byos-$(date +%Y%m%d-%H%M%S)"
    
    print_success "BYOS deployment completed"
}

# Function to deploy regular scenario
deploy_regular() {
    print_info "Deploying regular scenario..."
    print_info "Creating new storage account: $NEW_STORAGE_NAME"
    
    # Create deployment
    az deployment group create \
        --resource-group "$TARGET_RG" \
        --template-file template.json \
        --parameters \
            StorageAccount="$NEW_STORAGE_NAME" \
            UseExistingStorageAccount=false \
            IsByosEnabledDeployment=false \
            AzureSpeechServicesKey="$SPEECH_KEY" \
            AzureSpeechServicesRegion="$SPEECH_REGION" \
            Locale="$LOCALE" \
            DeploymentId="$DEPLOYMENT_ID" \
        --name "speech-transcription-regular-$(date +%Y%m%d-%H%M%S)"
    
    print_success "Regular deployment completed"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [byos|regular] [options]"
    echo ""
    echo "Scenarios:"
    echo "  byos     - Deploy using existing storage account (BYOS)"
    echo "  regular  - Deploy with new storage account"
    echo ""
    echo "Required Environment Variables:"
    echo "  TARGET_RG      - Target resource group name"
    echo "  SPEECH_KEY     - Azure Speech Services key"
    echo "  SPEECH_REGION  - Azure Speech Services region"
    echo ""
    echo "Optional Environment Variables:"
    echo "  LOCALE         - Speech recognition locale (default: en-US | English (United States))"
    echo "  DEPLOYMENT_ID  - Custom deployment ID (default: timestamp)"
    echo ""
    echo "For BYOS scenario:"
    echo "  EXISTING_STORAGE_NAME - Name of existing storage account"
    echo "  EXISTING_STORAGE_RG   - Resource group of existing storage"
    echo ""
    echo "For Regular scenario:"
    echo "  NEW_STORAGE_NAME      - Name for new storage account"
    echo ""
    echo "Examples:"
    echo "  # BYOS deployment"
    echo "  export TARGET_RG=\"my-rg\""
    echo "  export SPEECH_KEY=\"your-key\""
    echo "  export SPEECH_REGION=\"westus\""
    echo "  export EXISTING_STORAGE_NAME=\"existingstorage\""
    echo "  export EXISTING_STORAGE_RG=\"speech-rg\""
    echo "  $0 byos"
    echo ""
    echo "  # Regular deployment"
    echo "  export TARGET_RG=\"my-rg\""
    echo "  export SPEECH_KEY=\"your-key\""
    echo "  export SPEECH_REGION=\"westus\""
    echo "  export NEW_STORAGE_NAME=\"newstorage123\""
    echo "  $0 regular"
}

# Main script
main() {
    # Set defaults
    LOCALE="${LOCALE:-en-US | English (United States)}"
    DEPLOYMENT_ID="${DEPLOYMENT_ID:-$(date +%Y%m%d%H%M%S)}"
    
    # Check command line arguments
    if [ $# -eq 0 ]; then
        show_usage
        exit 1
    fi
    
    SCENARIO=$1
    
    case $SCENARIO in
        "byos")
            check_prerequisites
            validate_parameters "byos"
            deploy_byos
            ;;
        "regular")
            check_prerequisites
            validate_parameters "regular"
            deploy_regular
            ;;
        "help"|"-h"|"--help")
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown scenario: $SCENARIO"
            show_usage
            exit 1
            ;;
    esac
    
    print_success "Deployment script completed successfully!"
    print_info "Check the Azure Portal for deployment status and resources."
}

# Run main function
main "$@"