#!/bin/bash
# DDoS-монитор для SCP:SL. Читает /proc/net/dev + счётчики iptables.
# Один алерт на эпизод, с пиковой скоростью и уровнем.

WEBHOOK="https://discord.com/api/webhooks/1530205583511130164/2g0zMlMTOmi09TMdOOYL0XPUJZ0FJPQDkYP2_Ue40e_54RDTnRtbT0kSnsifrp9YZUSW"
IFACE="eth0"
HOSTNAME_TAG="$(hostname)"

# --- Пороги (тюнингуй под свой сервер) ---
FLOOR_MBIT=40          # ниже 40 Мбит/с не считаем атакой (норм. игра ~1-5 Мбит/с)
FLOOR_PPS=30000        # либо аномальный pps (udp-free шлёт кучу мелких пакетов)
SUSTAIN=3              # сек подряд выше порога = атака (анти false-positive)
NIC_CAPACITY_GBIT=10   # ёмкость аплинка. Выше -> канал забит -> "не отражена"

read_bytes(){ awk -v i="$IFACE" '$0~i":"{gsub(/.*:/,"");print $1" "$2}' /proc/net/dev; }
# rx_bytes rx_packets
dropped_pkts(){ iptables -nvxL SL-PreAuth 2>/dev/null | awk '/DROP/{s+=$1} END{print s+0}'; }

level_name(){ case "$1" in 1)echo "Лёгкая атака";;2)echo "Средняя атака";;3)echo "Сильная атака";;4)echo "Критическая атака";;esac; }
level_color(){ case "$1" in 1)echo 16776960;;2)echo 16744192;;3)echo 15158332;;4)echo 9109504;;esac; } # yellow/orange/red/darkred

classify(){ # $1 = peak Gbit/s (float)
  awk -v g="$1" 'BEGIN{ if(g<1)print 1; else if(g<2)print 2; else if(g<5)print 3; else print 4 }'
}

send_alert(){ # $1 level  $2 peak_gbit  $3 reflected(0/1)
  local lvl="$1" peak="$2" refl="$3"
  local name; name="$(level_name "$lvl")"
  local color; color="$(level_color "$lvl")"
  local status; if [ "$refl" -eq 1 ]; then status="Атака была **отражена** ✅"; else status="Атака **не отражена** ❌ (канал перегружен — нужен upstream-скраббинг)"; fi
  # человекочитаемая скорость
  local speed
  speed=$(awk -v g="$peak" 'BEGIN{ if(g<1)printf "%.0f mb/s", g*1000; else printf "%.2f gb/s", g }')

  local payload
  payload=$(cat <<JSON
{"embeds":[{
  "title":"$name",
  "color":$color,
  "description":"**[ДДОС $lvl уровня]**\n$status\nПиковая скорость атаки: **$speed**",
  "footer":{"text":"Сервер: $HOSTNAME_TAG"},
  "timestamp":"$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}]}
JSON
)
  curl -s -H "Content-Type: application/json" -X POST -d "$payload" "$WEBHOOK" >/dev/null
}

echo "[*] Мониторинг запущен на $IFACE"
prev=($(read_bytes)); prev_drop=$(dropped_pkts)
above=0; peak_bps=0; peak_drop=0; total_pkts=0

while sleep 1; do
  cur=($(read_bytes)); cur_drop=$(dropped_pkts)
  d_bytes=$(( ${cur[0]} - ${prev[0]} ))
  d_pkts=$((  ${cur[1]} - ${prev[1]} ))
  d_drop=$((  cur_drop - prev_drop ))
  prev=("${cur[@]}"); prev_drop=$cur_drop
  (( d_bytes<0 )) && continue

  bps=$(( d_bytes * 8 ))
  mbit=$(( bps / 1000000 ))

  if (( mbit >= FLOOR_MBIT || d_pkts >= FLOOR_PPS )); then
     above=$((above+1))
     (( bps  > peak_bps  )) && peak_bps=$bps
     (( d_drop > peak_drop )) && peak_drop=$d_drop
     total_pkts=$((total_pkts + d_pkts))
  else
     if (( above >= SUSTAIN )); then
        # эпизод закончился -> считаем итог
        peak_gbit=$(awk -v b="$peak_bps" 'BEGIN{printf "%.3f", b/1000000000}')
        lvl=$(classify "$peak_gbit")
        # отражена? канал не забит И большую часть pps дропнули
        refl=0
        cap_ok=$(awk -v g="$peak_gbit" -v c="$NIC_CAPACITY_GBIT" 'BEGIN{print (g < c*0.9)?1:0}')
        drop_ratio=$(awk -v d="$peak_drop" -v t="$total_pkts" 'BEGIN{print (t>0 && d/(t/'$above')>0.5)?1:0}')
        if [ "$cap_ok" -eq 1 ]; then refl=1; fi
        send_alert "$lvl" "$peak_gbit" "$refl"
        echo "[!] Эпизод: уровень $lvl, пик ${peak_gbit} Gbit/s, reflected=$refl"
     fi
     above=0; peak_bps=0; peak_drop=0; total_pkts=0
  fi
done