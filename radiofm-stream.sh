#!/bin/bash
URL="http://pub0301.101.ru/stream/air/aac/64/100"
OUT="/home/scpsl/.config/EXILED/Configs/EventHUD/Audio/RadioFM"

mkdir -p "$OUT"
rm -f "$OUT"/*.ogg

exec ffmpeg -hide_banner -loglevel error \
  -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 \
  -i "$URL" \
  -vn -c:a libvorbis -ar 48000 -ac 1 -b:a 64k \
  -f segment -segment_time 20 -reset_timestamps 1 -strftime 1 \
  "$OUT/%Y%m%d-%H%M%S.ogg"