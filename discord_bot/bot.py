import discord
from discord.ext import commands
import json
import asyncio
import logging
from typing import Optional
from datetime import datetime
from aiohttp import web

# Setup logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Load config
with open('config.json', 'r', encoding='utf-8') as f:
    config = json.load(f)

BOT_TOKEN = config['bot_token']
CHANNEL_ID = config['channel_id']
ROLE_PINGS = config['role_pings']
MESSAGES = config['messages']
EMBED_SETTINGS = config['embed_settings']

# Bot setup
intents = discord.Intents.default()
intents.message_content = True  # Required for reading command content
intents.guilds = True           # Required for guild/channel access
# No other privileged intents needed

bot = commands.Bot(command_prefix='!', intents=intents)

# Store pending messages for editing
pending_messages = {}

def create_embed(event_type: str, **kwargs) -> discord.Embed:
    """Create a formatted embed with blue line for event announcements."""
    msg_config = MESSAGES[event_type]
    
    # Format description with provided kwargs
    description = msg_config['description'].format(**kwargs)
    
    embed = discord.Embed(
        title=msg_config['title'],
        description=description,
        color=msg_config['color']
    )
    
    # Add blue line on the right (using a zero-width space field)
    if EMBED_SETTINGS.get('use_blue_line', True):
        embed.add_field(
            name="\u200b",  # Zero-width space
            value="\u200b",
            inline=False
        )
    
    # Add footer
    embed.set_footer(text=msg_config.get('footer', ''))
    
    # Add timestamp
    if EMBED_SETTINGS.get('timestamp', True):
        embed.timestamp = datetime.utcnow()
    
    return embed

def get_role_ping(rp_type: str) -> str:
    """Get role ping for RP type."""
    return ROLE_PINGS.get(rp_type.upper(), '')

@bot.event
async def on_ready():
    logger.info(f'Bot logged in as {bot.user}')
    logger.info(f'Connected to {len(bot.guilds)} guilds')
    
    # Verify channel exists
    channel = bot.get_channel(CHANNEL_ID)
    if channel:
        logger.info(f'Found channel: {channel.name} ({channel.id})')
    else:
        logger.error(f'Channel {CHANNEL_ID} not found!')

@bot.command(name='test_prepare')
async def test_prepare(ctx, rp_type: str = "NONRP", *, event_name: str = "Тестовый ивент"):
    """Test prepare announcement: !test_prepare NRP Название ивента"""
    channel = bot.get_channel(CHANNEL_ID)
    if not channel:
        await ctx.send("❌ Channel not found!")
        return
    
    role_ping = get_role_ping(rp_type)
    embed = create_embed('prepare', 
        event_name=event_name,
        host_name=ctx.author.display_name,
        player_count=42,
        rp_type=rp_type.upper()
    )
    
    content = role_ping if role_ping else None
    msg = await channel.send(content=content, embed=embed)
    await ctx.send(f"✅ Prepare test sent! Message ID: {msg.id}")

@bot.command(name='test_start')
async def test_start(ctx, rp_type: str = "NONRP", *, event_name: str = "Тестовый ивент"):
    """Test start announcement: !test_start NRP Название ивента"""
    channel = bot.get_channel(CHANNEL_ID)
    if not channel:
        await ctx.send("❌ Channel not found!")
        return
    
    role_ping = get_role_ping(rp_type)
    embed = create_embed('start',
        event_name=event_name,
        host_name=ctx.author.display_name,
        player_count=42,
        rp_type=rp_type.upper(),
        prep_time="5 мин 30 сек"
    )
    
    content = role_ping if role_ping else None
    msg = await channel.send(content=content, embed=embed)
    await ctx.send(f"✅ Start test sent! Message ID: {msg.id}")

@bot.command(name='test_stop')
async def test_stop(ctx, rp_type: str = "NONRP", *, event_name: str = "Тестовый ивент"):
    """Test stop announcement: !test_stop NRP Название ивента"""
    channel = bot.get_channel(CHANNEL_ID)
    if not channel:
        await ctx.send("❌ Channel not found!")
        return
    
    role_ping = get_role_ping(rp_type)
    embed = create_embed('stop',
        event_name=event_name,
        host_name=ctx.author.display_name,
        player_count=42,
        rp_type=rp_type.upper(),
        duration="45 мин 12 сек"
    )
    
    content = role_ping if role_ping else None
    msg = await channel.send(content=content, embed=embed)
    await ctx.send(f"✅ Stop test sent! Message ID: {msg.id}")

# HTTP endpoint for C# plugin to trigger announcements
async def handle_prepare(request):
    """Handle prepare event from C# plugin."""
    try:
        data = await request.json()
        event_name = data.get('event_name', 'Неизвестный ивент')
        host_name = data.get('host_name', 'Неизвестно')
        player_count = data.get('player_count', 0)
        rp_type = data.get('rp_type', 'NONRP')
        
        channel = bot.get_channel(CHANNEL_ID)
        if not channel:
            return web.json_response({'error': 'Channel not found'}, status=500)
        
        role_ping = get_role_ping(rp_type)
        embed = create_embed('prepare',
            event_name=event_name,
            host_name=host_name,
            player_count=player_count,
            rp_type=rp_type
        )
        
        content = role_ping if role_ping else None
        msg = await channel.send(content=content, embed=embed)
        
        # Store for potential updates
        pending_messages['prepare'] = msg.id
        
        return web.json_response({'success': True, 'message_id': msg.id})
    except Exception as e:
        logger.error(f"Error in handle_prepare: {e}")
        return web.json_response({'error': str(e)}, status=500)

async def handle_start(request):
    """Handle start event from C# plugin."""
    try:
        data = await request.json()
        event_name = data.get('event_name', 'Неизвестный ивент')
        host_name = data.get('host_name', 'Неизвестно')
        player_count = data.get('player_count', 0)
        rp_type = data.get('rp_type', 'NONRP')
        prep_time = data.get('prep_time', '0 сек')
        
        channel = bot.get_channel(CHANNEL_ID)
        if not channel:
            return web.json_response({'error': 'Channel not found'}, status=500)
        
        role_ping = get_role_ping(rp_type)
        embed = create_embed('start',
            event_name=event_name,
            host_name=host_name,
            player_count=player_count,
            rp_type=rp_type,
            prep_time=prep_time
        )
        
        content = role_ping if role_ping else None
        msg = await channel.send(content=content, embed=embed)
        
        pending_messages['start'] = msg.id
        
        return web.json_response({'success': True, 'message_id': msg.id})
    except Exception as e:
        logger.error(f"Error in handle_start: {e}")
        return web.json_response({'error': str(e)}, status=500)

async def handle_stop(request):
    """Handle stop event from C# plugin."""
    try:
        data = await request.json()
        event_name = data.get('event_name', 'Неизвестный ивент')
        host_name = data.get('host_name', 'Неизвестно')
        player_count = data.get('player_count', 0)
        rp_type = data.get('rp_type', 'NONRP')
        duration = data.get('duration', '0 сек')
        
        channel = bot.get_channel(CHANNEL_ID)
        if not channel:
            return web.json_response({'error': 'Channel not found'}, status=500)
        
        role_ping = get_role_ping(rp_type)
        embed = create_embed('stop',
            event_name=event_name,
            host_name=host_name,
            player_count=player_count,
            rp_type=rp_type,
            duration=duration
        )
        
        content = role_ping if role_ping else None
        msg = await channel.send(content=content, embed=embed)
        
        pending_messages['stop'] = msg.id
        
        return web.json_response({'success': True, 'message_id': msg.id})
    except Exception as e:
        logger.error(f"Error in handle_stop: {e}")
        return web.json_response({'error': str(e)}, status=500)

async def start_web_server():
    """Start HTTP server for C# plugin communication."""
    app = web.Application()
    app.router.add_post('/api/event/prepare', handle_prepare)
    app.router.add_post('/api/event/start', handle_start)
    app.router.add_post('/api/event/stop', handle_stop)
    
    runner = web.AppRunner(app)
    await runner.setup()
    site = web.TCPSite(runner, 'localhost', 8080)
    await site.start()
    logger.info("HTTP server started on http://localhost:8080")

async def main():
    # Start HTTP server
    await start_web_server()
    
    # Start bot
    await bot.start(BOT_TOKEN)

if __name__ == '__main__':
    asyncio.run(main())