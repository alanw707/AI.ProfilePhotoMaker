#!/bin/bash

# Chrome Update and Cleanup Script for WSL

# Ensure script is run with sudo
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run with sudo"
   exit 1
fi

# Function to log messages
log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*"
}

# Remove existing Chrome packages
remove_chrome() {
    log "Removing existing Chrome packages..."
    
    # List of packages to remove
    local packages=(
        "google-chrome-stable"
        "google-chrome-unstable"
        "google-chrome-beta"
    )
    
    for pkg in "${packages[@]}"; do
        if dpkg -l | grep -q "$pkg"; then
            apt remove -y "$pkg"
            log "Removed $pkg"
        fi
    done
    
    # Additional cleanup
    apt purge -y "*chrome*"
    apt autoremove -y
    apt clean
}

# Main update process
update_chrome() {
    # Update package lists
    log "Updating package lists..."
    apt update
    
    # Kill any existing Chrome processes
    log "Stopping Chrome processes..."
    pkill chrome || true
    
    # Remove existing Chrome
    remove_chrome
    
    # Download Chrome
    log "Downloading Chrome..."
    wget -q https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb -O /tmp/chrome.deb
    
    # Install Chrome
    log "Installing Chrome..."
    dpkg -i /tmp/chrome.deb
    
    # Fix any dependency issues
    apt-get install -f -y
    
    # Verify installation
    if chrome_version=$(google-chrome --version); then
        log "Successfully installed $chrome_version"
    else
        log "ERROR: Chrome installation failed"
        exit 1
    fi
    
    # Clean up download
    rm -f /tmp/chrome.deb
    
    log "Chrome update complete!"
}

# Run the update process
update_chrome

exit 0