# Discord Bot for SCP:SL Event Announcements

This Python bot sends beautiful Discord embed announcements for SCP:SL events (Prepare, Start, Stop) with blue line formatting.

## Features

- 🎮 **Three event types**: Prepare, Start, Stop announcements
- 🎨 **Beautiful embeds** with blue line formatting
- ⚙️ **Fully configurable** messages, colors, and role pings
- 🔗 **HTTP API** for C# plugin integration (port 8080)
- 🎯 **Role pings** per RP type (NRP, RP, HRP, etc.)
- 🧪 **Test commands** for Discord testing

## Setup

### 1. Install dependencies
```bash
# Windows
setup.bat

# Linux/Mac
chmod +x setup.sh && ./setup.sh
```

### 2. Configure `config.json`
Edit `config.json` with your settings:
```json
{
  "bot_token": "YOUR_BOT_TOKEN_HERE",
  "channel_id": 123456789012345678,
  "role_pings": {
    "NRP": "<@&123456789012345678>",
    "RP": "<@&123456789012345679>",
    "HRP": "<@&123456789012345680>",
    "EVENT": "<@&123456789012345681>"
  },
  "embed_settings": {
    "use_blue_line": true,
    "timestamp": true
  }
}
```

### 3. Run the bot
```bash
# Windows
run.bat

# Linux/Mac
./run.sh

# Or directly
python bot.py
```

## Configuration

### Bot Token
Get your bot token from [Discord Developer Portal](https://discord.com/developers/applications).

### Channel ID
Right-click your Discord channel → Copy ID (Developer Mode must be enabled).

### Role Pings
Add role IDs for each RP type. Use `<@&ROLE_ID>` format for pings.

### Messages
Customize all messages in the `messages` section of `config.json`. Use placeholders:
- `{event_name}` - Event name
- `{host_name}` - Host nickname
- `{player_count}` - Player count
- `{rp_type}` - RP type (NRP, RP, HRP)
- `{prep_time}` - Preparation time (start event)
- `{duration}` - Event duration (stop event)

## C# Plugin Integration

The C# plugin sends HTTP requests to `http://localhost:8080/api/event/{prepare|start|stop}`.

Make sure the bot is running before starting events in-game.

### API Endpoints

| Endpoint | Method | Payload |
|----------|--------|---------|
| `/api/event/prepare` | POST | `event_name`, `host_name`, `player_count`, `rp_type` |
| `/api/event/start` | POST | `event_name`, `host_name`, `player_count`, `rp_type`, `prep_time` |
| `/api/event/stop` | POST | `event_name`, `host_name`, `player_count`, `rp_type`, `duration` |

## Discord Commands

| Command | Description |
|---------|-------------|
| `!test_prepare [rp_type] [event_name]` | Test prepare announcement |
| `!test_start [rp_type] [event_name]` | Test start announcement |
| `!test_stop [rp_type] [event_name]` | Test stop announcement |

Example: `!test_prepare NRP "Тестовый ивент"`

## Embed Appearance

Embeds feature:
- 🎨 Custom colors per event type (Blue=Prepare, Green=Start, Red=Stop)
- 📏 Blue vertical line on the right side
- 📝 Formatted fields with event info
- 🕐 Timestamp
- 👥 Role pings based on RP type

## Requirements

- Python 3.8+
- discord.py 2.3+
- aiohttp 3.9+

## License

MIT License - Feel free to modify for your server!