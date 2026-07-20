#!/bin/bash
echo "========================================"
echo "Discord Bot for SCP:SL Event Announcements"
echo "========================================"
echo ""
echo "Starting bot..."
echo "Make sure config.json is configured with your bot token and channel ID!"
echo ""
cd /home/discord_bot
source venv/bin/activate
python3 bot.py