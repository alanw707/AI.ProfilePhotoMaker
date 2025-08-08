#!/bin/bash
# Azure DevOps Self-hosted Agent Setup Script
# Configures a containerized or VM-based agent for AI.ProfilePhotoMaker builds

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
AGENT_VERSION="${1:-3.232.0}"
AGENT_NAME="${2:-ai-profilemaker-agent-$(hostname)}"
AGENT_POOL="${3:-Self-Hosted-Linux-Pool}"
WORK_DIR="${4:-/opt/azagent}"

echo -e "${BLUE}🔧 Azure DevOps Agent Setup for AI.ProfilePhotoMaker${NC}"
echo -e "${BLUE}===================================================${NC}"
echo ""
echo -e "${BLUE}Configuration:${NC}"
echo -e "  Agent Version: ${AGENT_VERSION}"
echo -e "  Agent Name: ${AGENT_NAME}"
echo -e "  Agent Pool: ${AGENT_POOL}"
echo -e "  Work Directory: ${WORK_DIR}"
echo ""

# Validate prerequisites
echo -e "${BLUE}🔍 Validating prerequisites...${NC}"

if [ "$EUID" -ne 0 ]; then
  echo -e "${RED}❌ ERROR: This script must be run as root${NC}"
  echo "Run: sudo $0"
  exit 1
fi

# Check if required environment variables are set
if [ -z "$AZURE_DEVOPS_URL" ]; then
  echo -e "${RED}❌ ERROR: AZURE_DEVOPS_URL environment variable not set${NC}"
  echo "Set: export AZURE_DEVOPS_URL='https://dev.azure.com/your-org'"
  exit 1
fi

if [ -z "$AZURE_DEVOPS_TOKEN" ]; then
  echo -e "${RED}❌ ERROR: AZURE_DEVOPS_TOKEN environment variable not set${NC}"
  echo "Create a Personal Access Token with 'Agent Pools (read, manage)' scope"
  echo "Set: export AZURE_DEVOPS_TOKEN='your-pat-token'"
  exit 1
fi

echo -e "${GREEN}✅ Prerequisites validated${NC}"

# Install system dependencies
echo -e "${BLUE}📦 Installing system dependencies...${NC}"

# Detect OS
if command -v apt-get > /dev/null; then
  # Ubuntu/Debian
  apt-get update
  
  # Install basic packages
  apt-get install -y \
    curl \
    wget \
    ca-certificates \
    gnupg \
    lsb-release \
    software-properties-common \
    apt-transport-https \
    jq \
    unzip
  
  echo -e "${GREEN}✅ System packages installed${NC}"
  
elif command -v yum > /dev/null; then
  # RHEL/CentOS
  yum update -y
  yum install -y \
    curl \
    wget \
    ca-certificates \
    gnupg2 \
    yum-utils \
    jq \
    unzip
  
  echo -e "${GREEN}✅ System packages installed${NC}"
else
  echo -e "${RED}❌ ERROR: Unsupported operating system${NC}"
  exit 1
fi

# Install Docker
echo -e "${BLUE}🐳 Installing Docker...${NC}"

if ! command -v docker > /dev/null; then
  if command -v apt-get > /dev/null; then
    # Ubuntu/Debian Docker installation
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" > /etc/apt/sources.list.d/docker.list
    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  elif command -v yum > /dev/null; then
    # RHEL/CentOS Docker installation
    yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo
    yum install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  fi
  
  systemctl start docker
  systemctl enable docker
  echo -e "${GREEN}✅ Docker installed and started${NC}"
else
  echo -e "${GREEN}✅ Docker already installed${NC}"
fi

# Install .NET 8
echo -e "${BLUE}🔨 Installing .NET 8...${NC}"

if ! command -v dotnet > /dev/null; then
  if command -v apt-get > /dev/null; then
    # Ubuntu/Debian .NET installation
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    apt-get update
    apt-get install -y dotnet-sdk-8.0
  elif command -v yum > /dev/null; then
    # RHEL/CentOS .NET installation
    rpm -Uvh https://packages.microsoft.com/config/centos/8/packages-microsoft-prod.rpm
    yum install -y dotnet-sdk-8.0
  fi
  
  echo -e "${GREEN}✅ .NET 8 installed${NC}"
else
  echo -e "${GREEN}✅ .NET already installed${NC}"
fi

# Install Node.js 18
echo -e "${BLUE}📦 Installing Node.js 18...${NC}"

if ! command -v node > /dev/null || [ "$(node -v | cut -d. -f1 | cut -dv -f2)" -lt 18 ]; then
  curl -fsSL https://deb.nodesource.com/setup_18.x | bash -
  if command -v apt-get > /dev/null; then
    apt-get install -y nodejs
  elif command -v yum > /dev/null; then
    yum install -y nodejs
  fi
  
  echo -e "${GREEN}✅ Node.js 18 installed${NC}"
else
  echo -e "${GREEN}✅ Node.js already installed${NC}"
fi

# Install Azure CLI
echo -e "${BLUE}☁️ Installing Azure CLI...${NC}"

if ! command -v az > /dev/null; then
  curl -sL https://aka.ms/InstallAzureCLIDeb | bash
  echo -e "${GREEN}✅ Azure CLI installed${NC}"
else
  echo -e "${GREEN}✅ Azure CLI already installed${NC}"
fi

# Create agent user
echo -e "${BLUE}👤 Setting up agent user...${NC}"

if ! id "azureagent" &>/dev/null; then
  useradd -m -s /bin/bash azureagent
  usermod -aG docker azureagent
  echo -e "${GREEN}✅ Agent user created${NC}"
else
  echo -e "${GREEN}✅ Agent user already exists${NC}"
fi

# Create agent directory
echo -e "${BLUE}📁 Setting up agent directory...${NC}"

mkdir -p "$WORK_DIR"
chown azureagent:azureagent "$WORK_DIR"

# Download and extract agent
echo -e "${BLUE}⬇️ Downloading Azure DevOps agent...${NC}"

AGENT_URL="https://vstsagentpackage.azureedge.net/agent/${AGENT_VERSION}/vsts-agent-linux-x64-${AGENT_VERSION}.tar.gz"

cd "$WORK_DIR"
sudo -u azureagent wget -O "vsts-agent-linux-x64-${AGENT_VERSION}.tar.gz" "$AGENT_URL"
sudo -u azureagent tar zxvf "vsts-agent-linux-x64-${AGENT_VERSION}.tar.gz"
sudo -u azureagent rm "vsts-agent-linux-x64-${AGENT_VERSION}.tar.gz"

echo -e "${GREEN}✅ Agent downloaded and extracted${NC}"

# Install agent dependencies
echo -e "${BLUE}🔧 Installing agent dependencies...${NC}"

cd "$WORK_DIR"
sudo -u azureagent bash -c "./bin/installdependencies.sh"

# Configure agent
echo -e "${BLUE}⚙️ Configuring Azure DevOps agent...${NC}"

sudo -u azureagent bash -c "./config.sh \
  --unattended \
  --url '$AZURE_DEVOPS_URL' \
  --auth pat \
  --token '$AZURE_DEVOPS_TOKEN' \
  --pool '$AGENT_POOL' \
  --agent '$AGENT_NAME' \
  --work '_work' \
  --replace \
  --acceptTeeEula"

if [ $? -eq 0 ]; then
  echo -e "${GREEN}✅ Agent configured successfully${NC}"
else
  echo -e "${RED}❌ Agent configuration failed${NC}"
  exit 1
fi

# Install agent as service
echo -e "${BLUE}🔄 Installing agent service...${NC}"

sudo ./svc.sh install azureagent
sudo ./svc.sh start

if systemctl is-active --quiet vsts-agent-*.service; then
  echo -e "${GREEN}✅ Agent service installed and started${NC}"
else
  echo -e "${RED}❌ Agent service failed to start${NC}"
  exit 1
fi

# Verify installation
echo -e "${BLUE}🔍 Verifying installation...${NC}"

echo -e "${BLUE}System Information:${NC}"
echo -e "  OS: $(lsb_release -d | cut -f2 || cat /etc/os-release | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"')"
echo -e "  Docker: $(docker --version)"
echo -e "  .NET: $(dotnet --version)"
echo -e "  Node.js: $(node --version)"
echo -e "  NPM: $(npm --version)"
echo -e "  Azure CLI: $(az --version | head -1)"

echo ""
echo -e "${GREEN}🎉 Azure DevOps agent setup completed successfully!${NC}"
echo ""
echo -e "${BLUE}Agent Information:${NC}"
echo -e "  Name: ${AGENT_NAME}"
echo -e "  Pool: ${AGENT_POOL}"
echo -e "  Directory: ${WORK_DIR}"
echo -e "  Service: vsts-agent-*.service"
echo ""
echo -e "${BLUE}Management Commands:${NC}"
echo -e "  Status: ${YELLOW}sudo systemctl status vsts-agent-*.service${NC}"
echo -e "  Stop:   ${YELLOW}sudo systemctl stop vsts-agent-*.service${NC}"
echo -e "  Start:  ${YELLOW}sudo systemctl start vsts-agent-*.service${NC}"
echo -e "  Logs:   ${YELLOW}sudo journalctl -u vsts-agent-*.service -f${NC}"
echo ""
echo -e "${BLUE}Next Steps:${NC}"
echo -e "1. Verify agent appears in Azure DevOps → Organization Settings → Agent pools → ${AGENT_POOL}"
echo -e "2. Test with a simple pipeline run"
echo -e "3. Configure any additional tools or credentials as needed"
echo ""
echo -e "${GREEN}Agent is ready for AI.ProfilePhotoMaker builds!${NC}"