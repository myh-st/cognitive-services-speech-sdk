param(
    [string]$action
)

function Is-DotNetMeetRequirement {
    param (
        [string]$dotnetPath = "dotnet"   # Accept dotnet executable path as an argument. Default to "dotnet" if no value is provided
    )

    $requiredVersion = "8.0"

    # Check if dotnet is found at the provided path and if the version is below the required version
    if (-not  (Get-Command $dotnetPath -ErrorAction SilentlyContinue)) {
        return $false
    }

    # Get the dotnet version using the provided path
    $dotnetVersion = & $dotnetPath --version
    if ([version]$dotnetVersion -lt [version]$requiredVersion) {
        Write-Host "Dotnet version is below the required version ($requiredVersion). Found version: $dotnetVersion" -ForegroundColor Red
        return $false
    }

    return $true
}

$dotnetInstallationTempDirectory = "$env:TEMP\dotnet"
$dotnetTempPath = Join-Path $dotnetInstallationTempDirectory "dotnet.exe"

$dotnetExe = "dotnet"
if (-not (Is-DotNetMeetRequirement -dotnetPath $dotnetExe)) {
    $dotnetExe = $dotnetTempPath
}

function Install-DotNet8 {
    Write-Host "Installing .NET SDK 8.0..."

    Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
    if (-not $?) {
        Write-Host "Failed to download dotnet-install.ps1, exiting..." -ForegroundColor Red
        exit 1
    }

    & .\dotnet-install.ps1 -InstallDir $dotnetInstallationTempDirectory -Version 8.0.403
    if (-not $?) {
        Write-Host "Failed to install .NET SDK, exiting..." -ForegroundColor Red
        exit 1
    }

    [Environment]::SetEnvironmentVariable("PATH", "$env:PATH;$dotnetInstallationTempDirectory", [System.EnvironmentVariableTarget]::User)
    Write-Host "Added .NET SDK to PATH."

    # Retrieve and print the updated PATH to verify the addition
    $updatedPath = [Environment]::GetEnvironmentVariable("PATH", [System.EnvironmentVariableTarget]::User)
    Write-Host "Updated PATH: $updatedPath"

    Write-Host ".NET 8.0 installed successfully." -ForegroundColor Green
    Remove-Item -Force dotnet-install.ps1
}

if ($action -eq "configure") {
    if (-not (Is-DotNetMeetRequirement -dotnetPath $dotnetExe)) {
        Install-DotNet8
    } else {
        Write-Host ".NET SDK 8.0 is already installed." -ForegroundColor Green
    }
}
elseif ($action -eq "build") {
    Write-Host "Running command: $dotnetExe build .\post-call-analytics.sln" -ForegroundColor Cyan
    & $dotnetExe build .\post-call-analytics.sln

    # Check if the command was successful
    if (-not $?) {
        Write-Host "Build failed, exiting with status 1." -ForegroundColor Red
        exit 1
    }
}
elseif ($action -eq "run") {
    # Define the path to your .env file
    $envFilePath = ".env/.env.dev"

    if (Test-Path $envFilePath) {
        Get-Content -Path $envFilePath | ForEach-Object {
            # Ignore empty lines and lines that start with `#` (comments)
            if ($_ -and $_ -notmatch '^\s*#') {
                # Split each line into key and value
                $parts = $_ -split '=', 2
                $key = $parts[0].Trim()
                $value = $parts[1].Trim()

                # Set the environment variable
                [System.Environment]::SetEnvironmentVariable($key, $value)
            }
        }
    }
    else {
        Write-Host "File not found: $envFilePath. You can create one to set environment variables or manually set secrets in environment variables."
    }

    [System.Environment]::SetEnvironmentVariable("SPEECH_KEY", $env:SPEECH_RESOURCE_KEY)
    [System.Environment]::SetEnvironmentVariable("SPEECH_REGION", $env:SERVICE_REGION)

    $aoaiDeploymentName = Read-Host "Please enter the name of your Azure OpenAI deployment name (hit Enter to skip)"
    if ([string]::IsNullOrEmpty($aoaiDeploymentName)) {
        [System.Environment]::SetEnvironmentVariable("AOAI_KEY", "")
        [System.Environment]::SetEnvironmentVariable("AOAI_ENDPOINT", "")
        [System.Environment]::SetEnvironmentVariable("AOAI_DEPLOYMENT_NAME", "")

    } else {
        [System.Environment]::SetEnvironmentVariable("AOAI_KEY", $env:SPEECH_RESOURCE_KEY)
        [System.Environment]::SetEnvironmentVariable("AOAI_ENDPOINT", "https://$env:CUSTOM_SUBDOMAIN_NAME.openai.azure.com/")
        [System.Environment]::SetEnvironmentVariable("AOAI_DEPLOYMENT_NAME", $aoaiDeploymentName)
    }
    Write-Host "Environment variables set:`nSPEECH_KEY=$env:SPEECH_KEY`nSPEECH_REGION=$env:SPEECH_REGION`nAOAI_KEY=$env:AOAI_KEY`nAOAI_ENDPOINT=$env:AOAI_ENDPOINT`nAOAI_DEPLOYMENT_NAME=$env:AOAI_DEPLOYMENT_NAME"

    $inputFile = Read-Host "Please enter the path to the input audio file"

    & $dotnetExe run --project .\post-call-analytics.csproj $inputFile

    # Check if the command was successful
    if (-not $?) {
        Write-Host "Build failed, exiting with status 1." -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "Invalid action: $action" -ForegroundColor Red
    Write-Host "Usage: {configure | build | run}" -ForegroundColor Yellow
    exit 1
}