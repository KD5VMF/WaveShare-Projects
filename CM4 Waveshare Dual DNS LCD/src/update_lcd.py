#!/usr/bin/env python3
import os
import sys
import time
import re
from PIL import Image, ImageDraw, ImageFont

LCD_LIB_DIR = os.environ.get("LCD_LIB_DIR", "/home/sysop/CM4-NAS-Double-Deck_Demo/RaspberryPi")
LCD_ROTATE = int(os.environ.get("LCD_ROTATE", "180"))
UPDATE_LOG = os.environ.get("UPDATE_LOG", "/var/log/cm4-safe-nightly-update.log")
UPDATE_STATUS = os.environ.get("UPDATE_STATUS", "/run/cm4-safe-update.status")
TARGET_FPS = float(os.environ.get("TARGET_FPS", "2"))

sys.path.insert(0, LCD_LIB_DIR)

try:
    from lib import LCD_2inch
except Exception as e:
    print("ERROR: Could not import LCD_2inch.py")
    print("LCD_LIB_DIR =", LCD_LIB_DIR)
    print(e)
    sys.exit(1)

W, H = 320, 240

FONT_BIG = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 28)
FONT_MED = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 16)
FONT_SM = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 12)
FONT_TINY = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 10)

def read_status():
    try:
        with open(UPDATE_STATUS, "r", errors="replace") as f:
            return f.read().strip()
    except Exception:
        return "STARTING"

def tail_log(lines=7):
    try:
        with open(UPDATE_LOG, "r", errors="replace") as f:
            data = f.read().splitlines()
    except Exception:
        return ["waiting for update log..."]

    cleaned = []
    for line in data[-80:]:
        line = line.strip()
        if not line:
            continue

        line = re.sub(r"^Get:\d+ ", "GET ", line)
        line = re.sub(r"^Hit:\d+ ", "HIT ", line)
        line = line.replace("Preparing to unpack", "Prep")
        line = line.replace("Unpacking", "Unpack")
        line = line.replace("Setting up", "Setup")
        line = line.replace("Processing triggers for", "Trigger")
        line = line.replace("Reading package lists...", "Reading lists")
        line = line.replace("Building dependency tree...", "Dependency tree")
        line = line.replace("Reading state information...", "State info")
        line = line.replace("Calculating upgrade...", "Calculating")
        line = line.replace("After this operation,", "After:")

        if len(line) > 42:
            line = line[:41] + "…"

        cleaned.append(line)

    return cleaned[-lines:] if cleaned else ["waiting for update activity..."]

def status_color(status):
    s = status.upper()

    if "FAIL" in s or "ERROR" in s or "ABORT" in s:
        return (255, 50, 40)

    if "REBOOT" in s:
        return (255, 190, 40)

    if "UPGRADE" in s or "INSTALL" in s:
        return (80, 220, 255)

    if "SYNC" in s or "CONFIG" in s:
        return (255, 230, 80)

    return (80, 255, 130)

def draw_screen():
    status = read_status()
    c = status_color(status)
    lines = tail_log(7)

    img = Image.new("RGB", (W, H), (0, 0, 0))
    draw = ImageDraw.Draw(img)

    for y in range(H):
        shade = 4 + int(y * 0.035)
        draw.line((0, y, W, y), fill=(shade, shade + 3, shade + 12))

    draw.rectangle((0, 0, W, 34), fill=(2, 6, 12))
    draw.line((0, 34, W, 34), fill=c)

    draw.text((8, 5), "CM4 UPDATE", fill=c, font=FONT_MED)
    draw.text((230, 7), time.strftime("%H:%M:%S"), fill=(255, 255, 255), font=FONT_SM)

    draw.rounded_rectangle((10, 45, 310, 96), radius=8, outline=c, fill=(0, 0, 0), width=2)

    short = status.upper()
    if len(short) > 22:
        short = short[:21] + "…"

    draw.text((22, 57), short, fill=(255, 255, 255), font=FONT_BIG)

    t = time.time()
    bar_x = 14
    bar_y = 105
    bar_w = 292
    draw.rectangle((bar_x, bar_y, bar_x + bar_w, bar_y + 10), outline=(120, 120, 120), fill=(0, 0, 0))

    if "FAIL" in status.upper() or "ERROR" in status.upper() or "ABORT" in status.upper():
        fill_w = bar_w
    else:
        fill_w = int(((t * 45) % bar_w))

    draw.rectangle((bar_x + 1, bar_y + 1, bar_x + fill_w, bar_y + 9), fill=c)

    draw.rounded_rectangle((10, 124, 310, 232), radius=8, outline=(70, 130, 190), fill=(0, 0, 0), width=1)
    draw.text((18, 130), "RECENT ACTIVITY", fill=(120, 220, 255), font=FONT_TINY)

    y = 146
    for line in lines:
        draw.text((18, y), line, fill=(255, 255, 255), font=FONT_TINY)
        y += 12

    return img

def show(disp, img):
    if LCD_ROTATE in (90, 180, 270):
        img = img.rotate(LCD_ROTATE, expand=True)
    disp.ShowImage(img)

def main():
    disp = LCD_2inch.LCD_2inch()
    disp.Init()
    disp.clear()

    frame_time = 1.0 / max(1.0, TARGET_FPS)

    while True:
        start = time.time()
        img = draw_screen()
        show(disp, img)

        delay = frame_time - (time.time() - start)
        if delay > 0:
            time.sleep(delay)

if __name__ == "__main__":
    main()
