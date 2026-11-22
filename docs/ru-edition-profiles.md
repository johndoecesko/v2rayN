# Профили «Скорость» и «Скрытность» для RU-edition

Для устойчивой работы в РФ удобно держать два профиля и переключаться между ними:

- **Профиль «Скорость»** — Hysteria2 (sing-box) по прокладке с несколькими каналами; оптимален для обычного трафика.
- **Профиль «Скрытность»** — VLESS + REALITY (chrome, фрагментация, XHTTP) — для обхода жёсткой сети.

Переключение: меню «Серверы → Профили» или горячая клавиша «Make Active» (Enter в списке) / перетаскивание по полю «Sort».

## Профиль «Скорость» — Hysteria2 (sing-box)

1. Ядро: **sing-box** (меню → Настройки → ядра → sing-box → загрузить).
2. Серверы → Добавить → **Hysteria2**.
3. Заполнение:
   - Address/Port: ваш VPS (3/4-й порт, например 443).
   - Password: ключ Hysteria2.
   - Port Hopping Interval (6–30): 8–15 (опционально, добавляет ротацию портов).
   - Congestion Control: **bbr** (или ccf).
   - (необязательно) Mux Padding: enabled.
4. Правила маршрутизации: RU-direct пресет (или **RU direct** вручную).
5. Health-checks: включить (LeastPing), таймаут 10–20 с.

## Профиль «Скрытность» — VLESS + REALITY + XHTTP (Xray)

1. Ядро: **Xray**.
2. Серверы → Добавить → **VLESS**.
   - Flow: `xtls-rprx-vision`.
   - Security: `reality`; SNI: случайный существующий сайт-маска.
   - Fingerprint: `chrome` (по умолчанию уже chrome в этой сборке).
   - XHTTP: Host/Path по умолчанию, Mode `auto`.
3. Глобальные: `EnableFragment = true` (по умолчанию уже).
4. Правила маршрутизации: RU-direct пресет.
5. Health-checks: включить (LeastPing), таймаут 10–20 с.

## TUIC — ещё более стабильный альтернативный профиль

- Серверы → Добавить → TUIC.
- Поддерживает UUID + Password, congestion control (cubic/bbr).
- Идеален как запасной вариант вместо Hysteria2.

## Переключатель скорость/скрытность (рекомендуемый порядок)

1. Создайте профиль для каждой серверной группы (Policy Group).
2. Активный профиль: «Скорость» днём, «Скрытность» при сбое или в критичных сетях.
3. Системный прокси: всегда 127.0.0.1:10808 — устойчив, при переключении профилей переподключение не нужно.

## Когда включать «Скрытность»

- При замедлении или обрыве соединения на Xray — переключитесь на Hysteria2/TUIC.
- При подозрении на детектирование прокси на TCP-уровне (RealSpeed ниже 10 Мбит, высокий RTT) — включите EnableFinalFragment.

## Health-checks / автовыбор (для обоих профилей)

- LeastPing в policy group; таймаут 10–20 с; URL — `https://www.gstatic.com/generate_204`.
- Авто-переключение срабатывает на сбой соединения; лучший по задержке выбирается при переподключении.


## NaïveProxy (поддерживается в UI)

- Ядро: **Xray** (встроенный ECoreType.naiveproxy) или **sing-box** (TUIC как альтернатива).
- Серверы → Добавить → **NaïveProxy**: Address/Port/Password, Protocol = `https` или `quic`.
- Настройте AEAD-cipher (Xray), or включите Mux+Padding в sing-box.

## ShadowTLS

- ShadowTLS **не встроен** в этот билд (ECoreType.shadowtls отсутствует).
- Для RU-edition функциональные аналоги: Xray + REALITY + XHTTP, sing-box + TUIC/Hysteria2.
- Если нужен ShadowTLS — используйте пользовательский `outbound` или обновите ядро с поддержкой.

## Padding (XHTTP / finalmask)

- **Xray**: добавьте настройки паддинга через `XhttpExtra` (JSON, согласуйте с документацией Xray) — noise-mask / tcp-level padding.
- **XRay finalmask** (в этой сборке): поддерживаются `fragment` (включено по умолчанию) и `noise` маски через XhttpExtra.
- **sing-box Mux**: включите `Mux Padding` (MuxEnabled → padding выбранное в UI).
