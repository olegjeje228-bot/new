#!/bin/bash
# SCP:SL network-layer firewall. Запускать от root.
# Слой ПОД твоей LabAPI-защитой (FUTURE-SL). Режет мусор до игры.
set -e

IFACE="eth0"        # <-- твой сетевой интерфейс (проверь: ip -br a)
GAME_PORT="7777"    # <-- порт SCP:SL сервера (Northwood default 7777)
SSH_PORT="22"

echo "[*] Настройка sysctl (анти-спуф + conntrack tuning)..."
sysctl -w net.ipv4.conf.all.rp_filter=1              >/dev/null  # reverse path filter: режет спуф
sysctl -w net.ipv4.conf.default.rp_filter=1          >/dev/null
sysctl -w net.ipv4.tcp_syncookies=1                  >/dev/null
sysctl -w net.ipv4.icmp_echo_ignore_broadcasts=1     >/dev/null
sysctl -w net.netfilter.nf_conntrack_max=1048576     >/dev/null
sysctl -w net.netfilter.nf_conntrack_udp_timeout=15  >/dev/null  # быстрее чистим UDP-мусор
sysctl -w net.netfilter.nf_conntrack_udp_timeout_stream=30 >/dev/null

echo "[*] Сброс старых правил SL..."
iptables -D INPUT -i "$IFACE" -p udp --dport "$GAME_PORT" -j SL-Filter 2>/dev/null || true
iptables -F SL-Filter 2>/dev/null || true
iptables -F SL-PreAuth 2>/dev/null || true
iptables -X SL-Filter 2>/dev/null || true
iptables -X SL-PreAuth 2>/dev/null || true

iptables -N SL-Filter
iptables -N SL-PreAuth

# --- Базовая гигиена (дёшево, без false-positive) ---
iptables -A SL-Filter -m conntrack --ctstate INVALID -j DROP
iptables -A SL-Filter -m conntrack --ctstate RELATED,ESTABLISHED -j ACCEPT
# Новое соединение -> проверка PreAuth
iptables -A SL-Filter -m conntrack --ctstate NEW -j SL-PreAuth
iptables -A SL-Filter -j DROP

# --- PreAuth chain ---
# 1) Уже в чёрном списке (спамил мусором) -> DROP на 300 сек
iptables -A SL-PreAuth -m recent --name slblack --rcheck --seconds 300 --hitcount 5 -j DROP

# 2) Анти-рандомайзер/анти-flood: не больше 8 НОВЫХ хендшейков за 10 сек с одного IP.
#    Живой игрок физически не открывает больше. Booter'ы/рандомайзеры — открывают тысячи.
iptables -A SL-PreAuth -m hashlimit \
    --hashlimit-name slnew --hashlimit-mode srcip \
    --hashlimit-above 8/10s --hashlimit-burst 8 \
    -m recent --name slblack --set -j DROP

# 3) ГЛАВНЫЙ фильтр: нет магического заголовка SCP:SL -> в чёрный список и DROP.
#    Легальный клиент ВСЕГДА шлёт |050d000000|. Мусор — нет.
iptables -A SL-PreAuth -m string ! --hex-string "|050d000000|" --algo kmp \
    -m recent --name slblack --set -j DROP

# 4) Валидный хендшейк с реальной платформой -> ACCEPT
iptables -A SL-PreAuth -m string --string "@steam"     --algo bm -j ACCEPT
iptables -A SL-PreAuth -m string --string "@northwood" --algo bm -j ACCEPT
iptables -A SL-PreAuth -m string --string "@discord"   --algo bm -j ACCEPT

# 5) Заголовок есть, но платформа кривая -> подозрительно, в чёрный список
iptables -A SL-PreAuth -m recent --name slblack --set -j DROP

# Повесить фильтр на игровой порт
iptables -I INPUT -i "$IFACE" -p udp --dport "$GAME_PORT" -j SL-Filter

echo "[+] Готово. Проверка: iptables -nvxL SL-Filter && iptables -nvxL SL-PreAuth"