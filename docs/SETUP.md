# AI.ProfilePhotoMaker Setup Guide

This document provides instructions for setting up the development environment for the AI.ProfilePhotoMaker project.

---

## Prerequisites

### Backend (.NET 8 API)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (or SQL Server Express)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or [VS Code](https://code.visualstudio.com/)

### Frontend (Angular)
- [Node.js](https://nodejs.org/) (v18 or later)
- [Angular CLI](https://angular.io/cli) (v19.x)
- [VS Code](https://code.visualstudio.com/) (recommended)

### AI Integration
- [Replicate.com](https://replicate.com/) account with API access
- API token with sufficient credits

---

## Development Environment Setup

### Option 1: Windows Setup

1. **Install Required Tools**
   ```powershell
   # Install Chocolatey (if not installed)
   Set-ExecutionPolicy Bypass -Scope Process -Force
   iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))
   
   # Install tools
   choco install dotnet-sdk -y
   choco install nodejs-lts -y
   choco install git -y
   choco install visualstudio2022community -y
   ```

2. **Install Angular CLI**
   ```powershell
   npm install -g @angular/cli
   ```

3. **Clone Repository**
   ```powershell
   git clone https://github.com/yourusername/AI.ProfilePhotoMaker.git
   cd AI.ProfilePhotoMaker
   ```

### Option 2: WSL Setup (Windows Subsystem for Linux)

1. **Install WSL**
   ```powershell
   # In PowerShell as Administrator
   wsl --install -d Ubuntu-22.04
   ```

2. **Install .NET 8 SDK in WSL**
   ```bash
   # In WSL terminal
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb

   sudo apt-get update
   sudo apt-get install -y apt-transport-https
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   ```

3. **Install Node.js and Angular CLI in WSL**
   ```bash
   # In WSL terminal
   curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
   sudo apt-get install -y nodejs

   # Verify Node installation
   node --version

   # Install Angular CLI globally
   npm install -g @angular/cli

   # Verify Angular CLI installation
   ng version
   ```

4. **Configure Git**
   ```bash
   git config --global user.name "Your Name"
   git config --global user.email "your.email@example.com"
   git config --global core.autocrlf input
   ```

5. **Clone Repository**
   ```bash
   # Set up SSH for GitHub first or use HTTPS with token
   mkdir -p ~/projects
   cd ~/projects
   git clone git@github.com:yourusername/AI.ProfilePhotoMaker.git
   cd AI.ProfilePhotoMaker
   ```

### Option 3: macOS Setup

1. **Install Required Tools**
   ```bash
   # Install Homebrew (if not installed)
   /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
   
   # Install tools
   brew install --cask dotnet-sdk
   brew install node
   brew install git
   brew install --cask visual-studio-code
   ```

2. **Install Angular CLI**
   ```bash
   npm install -g @angular/cli
   ```

3. **Clone Repository**
   ```bash
   git clone https://github.com/yourusername/AI.ProfilePhotoMaker.git
   cd AI.ProfilePhotoMaker
   ```

---

## Project Setup

### Backend Setup

1. **Restore NuGet Packages**
   ```bash
   cd AI.ProfilePhotoMaker.API
   dotnet restore
   ```

2. **Update Database**
   ```bash
   # Apply migrations to create the database
   dotnet ef database update
   ```

3. **Configure Replicate API**
   ```bash
   # Update appsettings.Development.json with your API keys
   # Example:
   ```
   ```json
   {
     "Replicate": {
       "ApiToken": "YOUR_API_TOKEN",
       "FluxTrainingModelId": "ostris/flux-dev-lora-trainer",
       "FluxGenerationModelId": "black-forest-labs/flux-dev",
       "WebhookSecret": "your-webhook-secret"
     },
     "AppBaseUrl": "https://localhost:5001"
   }
   ```

4. **Run the API**
   ```bash
   dotnet run
   ```
   The API will be available at `https://localhost:5001`

### Frontend Setup

1. **Install Dependencies**
   ```bash
   cd AI.ProfilePhotoMaker.UI
   npm install
   ```

2. **Configure Environment**
   ```bash
   # Update environment.ts with the correct API URL
   ```

3. **Run the Angular App**
   ```bash
   ng serve
   ```
   The application will be available at `http://localhost:4200`

---

## Development Workflow

1. **Backend Development**
   - Use Visual Studio 2022 or VS Code to edit the .NET 8 API
   - Implement new endpoints in controllers
   - Add services for business logic
   - Create/update models as needed

2. **Frontend Development**
   - Use Angular CLI to generate components, services, etc.
   - Follow Angular style guide for naming and structure
   - Implement responsive design using SASS

3. **Database Migrations**
   ```bash
   # Create a new migration
   dotnet ef migrations add MigrationName
   
   # Apply migrations
   dotnet ef database update
   ```

4. **Testing**
   - Backend: `dotnet test`
   - Frontend: `ng test`

---

## Webhook Testing

For testing webhooks locally, use ngrok:

1. **Install ngrok**
   ```bash
   # Windows (with Chocolatey)
   choco install ngrok
   
   # macOS
   brew install ngrok
   
   # Linux
   snap install ngrok
   ```

2. **Create a tunnel to your local API**
   ```bash
   ngrok http https://localhost:5001
   ```

3. **Update Replicate webhook URL**
   ```csharp
   // Use the ngrok URL in your code
   webhook = $"https://your-ngrok-url.ngrok.io/api/webhooks/replicate/training-complete"
   ```

---

## Troubleshooting

### Common Issues

1. **Database Connection Issues**
   - Verify connection string in `appsettings.Development.json`
   - Ensure SQL Server is running
   - Check if database exists

2. **Replicate API Integration**
   - Verify API token is valid and has credits
   - Check webhook URL is accessible from the internet
   - Monitor webhook requests with ngrok inspection UI

3. **Angular Errors**
   - Clear npm cache: `npm cache clean --force`
   - Remove node_modules: `rm -rf node_modules package-lock.json`
   - Reinstall: `npm install`

---

## Semantic Search Tools

This project uses semantic search tools (integrated with Copilot and other AI assistants) to:
- Help new developers onboard quickly
- Automate codebase and documentation analysis
- Provide context-aware troubleshooting and suggestions
- Keep documentation up to date with code changes

---

*Last updated: June 2023*