#!/bin/bash
echo "========================================"
echo "Discord Bot Setup for Ubuntu 24.04"
echo "========================================"
echo ""

# Update package list
echo "Updating package list..."
sudo apt update

# Install Python 3 and pip if not present
echo "Installing Python 3 and pip..."
sudo apt install -y python3 python3-pip python3-venv

# Create virtual environment (recommended)
echo "Creating virtual environment..."
python3 -m venv venv

# Activate virtual environment
echo "Activating virtual environment..."
source venv/bin/activate

# Upgrade pip
echo "Upgrading pip..."
pip install --upgrade pip

# Install dependencies
echo "Installing Python dependencies..."
pip install -r requirements.txt

echo ""
echo "========================================"
echo "Setup complete!"
echo "========================================"
echo ""
echo "To run the bot:"
echo "  source venv/bin/activate"
echo "  python3 bot.py"
echo ""
echo "Or simply run: ./run.sh"
echo ""
echo "Make sure to configure config.json with your bot token and channel ID first!"