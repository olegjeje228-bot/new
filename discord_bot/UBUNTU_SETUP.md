# Discord Bot for SCP:SL Event Announcements - Ubuntu 24.04 Setup

## Quick Start

```bash
# 1. Copy the discord_bot folder to /home/discord_bot on your Ubuntu server
# 2. Navigate to the directory
cd /home/discord_bot

# 3. Run setup script
chmod +x setup.sh
./setup.sh

# 4. Configure config.json with your bot token and channel ID
nano config.json

# 5. Run the bot
chmod +x run.sh
./run.sh
```

## Detailed Steps

### 1. Install Dependencies
```bash
sudo apt update
sudo apt install -y python3 python3-pip python3-venv git
```

### 2. Create Virtual Environment (Recommended)
```bash
python3 -m venv venv
source venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
```

### 3. Configure the Bot
Edit `config.json`:
```json
{
  "bot_token": "YOUR_BOT_TOKEN_FROM_DISCORD_DEVELOPER_PORTAL",
  "channel_id": 123456789012345678,
  "role_pings": {
    "NONRP": "<@&ROLE_ID>",
    "FUNRP": "<@&ROLE_ID>",
    "LIGHTRP": "<@&ROLE_ID>",
    "MEDIUMRP": "<@&ROLE_ID>",
    "HARDRP": "<@&ROLE_ID>",
    "FULLRP": "<@&ROLE_ID>"
  }
}
```

### 4. Run the Bot
```bash
# With virtual environment activated
cd /home/discord_bot
python3 bot.py

# Or use the run script
./run.sh
```

## Run as a Systemd Service (Production)

Create service file:
```bash
sudo nano /etc/systemd/system/scp-sl-discord-bot.service
```

Content:
```ini
[Unit]
Description=SCP:SL Discord Event Bot
After=network.target

[Service]
Type=simple
User=discord_bot
WorkingDirectory=/home/discord_bot
Environment=PATH=/home/discord_bot/venv/bin
ExecStart=/home/discord_bot/venv/bin/python bot.py
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable scp-sl-discord-bot
sudo systemctl start scp-sl-discord-bot
sudo systemctl status scp-sl-discord-bot
```

View logs:
```bash
sudo journalctl -u scp-sl-discord-bot -f
```

## Firewall (if needed)
```bash
# Allow HTTP API port for C# plugin communication
sudo ufw allow 8080/tcp
```

## Requirements
- Python 3.8+
- discord.py 2.3+
- aiohttp 3.9+
- Bot token with **Message Content Intent** enabled in Discord Developer Portal
- Bot invited to your server with permissions: Send Messages, Embed Links, View Channels, Mention Everyone

## C# Plugin Integration
The bot exposes HTTP API on `http://localhost:8080/api/event/{prepare|start|stop}`.
Configure your C# plugin to send POST requests to these endpoints.