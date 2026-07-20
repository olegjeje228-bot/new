@echo off
echo Installing Discord bot dependencies...
pip install -r requirements.txt
echo.
echo Setup complete! Edit config.json with your bot token and channel ID.
echo Then run: python bot.py
pause