# Setting Up AI.ProfilePhotoMaker in Windows Subsystem for Linux (WSL)

This guide provides detailed instructions for setting up the development environment for AI.ProfilePhotoMaker using Windows Subsystem for Linux (WSL).

---

## Why Use WSL?

WSL offers several advantages for .NET and Angular development:

- **Linux-like Environment**: Better compatibility with deployment targets
- **Performance**: File operations and Docker can be faster in WSL2
- **Better Terminal Experience**: Access to Linux shell commands
- **Cross-platform Testing**: Ensures your app works on Linux and Windows
- **Development Consistency**: Closer to production environment if deploying to Linux

---

## Prerequisites

1. Windows 10 (version 2004 or higher) or Windows 11
2. Administrator access to your computer

---

## Step 1: Install WSL

1. **Open PowerShell as Administrator** and run:

   ```powershell
   wsl --install -d Ubuntu-22.04
   ```

   This command installs WSL with Ubuntu 22.04 as the Linux distribution.

2. **Restart your computer** when prompted.

3. **Set up Ubuntu account**:
   - After restart, a Ubuntu console will open
   - Create a username and password when prompted

---

## Step 2: Install .NET 8 SDK in WSL

1. **Open your WSL terminal** and run the following commands:

   ```bash
   # Add Microsoft package repository
   wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   rm packages-microsoft-prod.deb

   # Install .NET SDK
   sudo apt-get update
   sudo apt-get install -y apt-transport-https
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   ```

2. **Verify installation**:

   ```bash
   dotnet --version
   # Should output 8.0.x
   ```

---

## Step 3: Install Node.js and Angular CLI

1. **Install Node.js**:

   ```bash
   # Install Node.js 18.x
   curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
   sudo apt-get install -y nodejs

   # Verify installation
   node --version  # Should output v18.x.x
   npm --version   # Should output 9.x.x
   ```

2. **Install Angular CLI**:

   ```bash
   # Install Angular CLI globally
   npm install -g @angular/cli

   # Verify installation
   ng version
   ```

---

## Step 4: Install SQL Server Tools

1. **Install SQL Server command-line tools**:

   ```bash
   # Import the public repository GPG keys
   curl https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -

   # Register the Microsoft SQL Server repository
   curl https://packages.microsoft.com/config/ubuntu/22.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list

   # Update package list and install tools
   sudo apt-get update
   sudo apt-get install -y mssql-tools unixodbc-dev

   # Add SQL tools to your path
   echo 'export PATH="$PATH:/opt/mssql-tools/bin"' >> ~/.bashrc
   source ~/.bashrc
   ```

2. **Note**: For database connection, you'll need to:
   - Connect to your Windows SQL Server instance using `host.docker.internal`
   - Or install SQL Server for Linux in WSL (more complex setup)

---

## Step 5: Configure Git

1. **Set up Git configuration**:

   ```bash
   # Set your name
   git config --global user.name "Your Name"

   # Set your email (use the same email as your GitHub account)
   git config --global user.email "your.email@example.com"

   # Set line ending behavior for cross-platform development
   git config --global core.autocrlf input
   ```

2. **Generate SSH key for GitHub**:

   ```bash
   # Generate a new SSH key
   ssh-keygen -t ed25519 -C "your.email@example.com"

   # Start the SSH agent
   eval "$(ssh-agent -s)"

   # Add your SSH key to the agent
   ssh-add ~/.ssh/id_ed25519

   # Display your public key (add this to GitHub)
   cat ~/.ssh/id_ed25519.pub
   ```

3. **Add the SSH key to your GitHub account**:
   - Go to GitHub → Settings → SSH and GPG keys
   - Click "New SSH key"
   - Paste your key and give it a title (e.g., "WSL Ubuntu")

---

## Step 6: Clone the Repository

1. **Create a projects directory**:

   ```bash
   mkdir -p ~/projects
   cd ~/projects
   ```

2. **Clone the repository using SSH**:

   ```bash
   git clone git@github.com:yourusername/AI.ProfilePhotoMaker.git
   cd AI.ProfilePhotoMaker
   ```

---

## Step 7: Set Up VS Code for WSL Development

1. **Install VS Code on Windows** (if not already installed):
   - Download from https://code.visualstudio.com/

2. **Install the Remote - WSL extension**:
   - Open VS Code
   - Go to Extensions (Ctrl+Shift+X)
   - Search for "Remote - WSL"
   - Click Install

3. **Open the project in VS Code**:

   ```bash
   # In WSL terminal, in your project directory
   code .
   ```

   This will open VS Code connected to your WSL environment.

4. **Install recommended VS Code extensions in WSL**:
   - C# Dev Kit
   - Angular Language Service
   - ESLint
   - EditorConfig for VS Code

---

## Step 8: Configure the Backend

1. **Restore NuGet packages**:

   ```bash
   cd AI.ProfilePhotoMaker.API
   dotnet restore
   ```

2. **Update database connection string**:

   Edit `appsettings.Development.json`:

   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=host.docker.internal,1433;Database=AI_ProfilePhotoMaker;User Id=sa;Password=YourPassword;TrustServerCertificate=True;"
   }
   ```

   This connects to SQL Server running on your Windows host.

3. **Update Replicate API settings**:

   ```json
   "Replicate": {
     "ApiToken": "YOUR_API_TOKEN",
     "FluxTrainingModelId": "ostris/flux-dev-lora-trainer",
     "FluxGenerationModelId": "black-forest-labs/flux-dev",
     "WebhookSecret": "your-webhook-secret"
   },
   "AppBaseUrl": "https://localhost:5001"
   ```

4. **Set up HTTPS development certificate**:

   ```bash
   dotnet dev-certs https --clean
   dotnet dev-certs https --trust
   ```

---

## Step 9: Configure the Frontend

1. **Install npm dependencies**:

   ```bash
   cd AI.ProfilePhotoMaker.UI
   npm install
   ```

2. **Configure environment**:

   Edit `src/environments/environment.development.ts`:

   ```typescript
   export const environment = {
     production: false,
     apiUrl: 'https://localhost:5001/api'
   };
   ```

---

## Step 10: Run the Application

1. **Run the backend**:

   ```bash
   # In one terminal
   cd AI.ProfilePhotoMaker.API
   dotnet run
   ```

2. **Run the frontend**:

   ```bash
   # In another terminal
   cd AI.ProfilePhotoMaker.UI
   ng serve
   ```

3. **Access the application**:
   - Backend API: https://localhost:5001
   - Swagger documentation: https://localhost:5001/swagger
   - Frontend application: http://localhost:4200

---

## Troubleshooting

### Common Issues and Solutions

1. **File Permission Issues**:

   ```bash
   # Fix ownership problems
   sudo chown -R $(whoami) ~/projects/AI.ProfilePhotoMaker
   ```

2. **Database Connection Issues**:
   - Ensure your SQL Server instance accepts remote connections
   - Check Windows Firewall settings for SQL Server port (default 1433)
   - Verify the connection string in appsettings.Development.json

3. **SSL Certificate Issues**:

   ```bash
   # Recreate development certificates
   dotnet dev-certs https --clean
   dotnet dev-certs https --trust
   ```

4. **Node.js/npm Issues**:

   ```bash
   # Clear npm cache and node_modules
   rm -rf node_modules package-lock.json
   npm cache clean --force
   npm install
   ```

5. **WSL Performance Issues**:
   - Add these lines to `~/.wslconfig` in your Windows home directory:
     ```
     [wsl2]
     memory=8GB
     processors=4
     ```
   - Restart WSL: `wsl --shutdown` in PowerShell, then reopen Ubuntu

---

## Working with WSL - Best Practices

1. **Keep code in the Linux filesystem**:
   - Store your project in `/home/username/` path, not in `/mnt/c/`
   - This improves file I/O performance significantly

2. **Use VS Code's Remote WSL extension**:
   - Opens VS Code directly connected to WSL
   - Ensures extensions run in Linux context

3. **Terminal choice**:
   - Use VS Code's integrated terminal (set to bash)
   - Or use Windows Terminal with Ubuntu profile

4. **Port forwarding**:
   - WSL automatically forwards ports to Windows
   - Access services on localhost from Windows browser

5. **Docker integration**:
   - Install Docker Desktop for Windows
   - Enable WSL integration in Docker Desktop settings

---

*Last updated: June 2023*