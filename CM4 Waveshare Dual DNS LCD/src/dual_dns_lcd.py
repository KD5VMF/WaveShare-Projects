#!/usr/bin/env python3
import os
import sys
import re
import json
import time
import base64
import socket
import subprocess
import urllib.request
import urllib.error
from PIL import Image, ImageDraw, ImageFont

def read_env_file(path):
    out = {}
    try:
        with open(path, "r") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                k, v = line.split("=", 1)
                out[k.strip()] = v.strip().strip('"').strip("'")
    except Exception:
        pass
    return out

CFG = {}
CFG.update(read_env_file("/etc/default/dual-dns-lcd"))
CFG.update(read_env_file("/etc/default/adguard-lcd"))

def cfg(name, default=""):
    return os.environ.get(name, CFG.get(name, default))

LCD_LIB_DIR = cfg("LCD_LIB_DIR", "/home/sysop/CM4-NAS-Double-Deck_Demo/RaspberryPi")
LCD_ROTATE = int(cfg("LCD_ROTATE", "180"))

DNS1_HOST = cfg("DNS1_HOST", "192.168.1.207")
DNS2_HOST = cfg("DNS2_HOST", "192.168.1.206")
DNS1_NAME = cfg("DNS1_NAME", "DNS1")
DNS2_NAME = cfg("DNS2_NAME", "DNS2")

DNS_TEST_NAME = cfg("DNS_TEST_NAME", "google.com")
SCAN_INTERVAL = int(cfg("SCAN_INTERVAL", "5"))
TARGET_FPS = float(cfg("TARGET_FPS", "4"))

ADGUARD_USER = cfg("ADGUARD_USER", "")
ADGUARD_PASSWORD = cfg("ADGUARD_PASSWORD", "")

sys.path.insert(0, LCD_LIB_DIR)

try:
    from lib import LCD_2inch
except Exception as e:
    print("ERROR: Could not import LCD_2inch.py")
    print("LCD_LIB_DIR =", LCD_LIB_DIR)
    print(e)
    sys.exit(1)

W, H = 320, 240

FONT_BIG = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 30)
FONT_MED = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 17)
FONT_SM = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 14)
FONT_TINY = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 10)

def run_cmd(cmd, timeout=3):
    try:
        p = subprocess.run(cmd, text=True, capture_output=True, timeout=timeout)
        return p.returncode, p.stdout.strip(), p.stderr.strip()
    except Exception as e:
        return 999, "", str(e)

def ping_ms(host):
    rc, out, err = run_cmd(["ping", "-c", "1", "-W", "1", host], timeout=2)
    if rc != 0:
        return None
    m = re.search(r"time=([\d.]+)", out)
    return float(m.group(1)) if m else 0.0

def tcp_port_open(host, port=53, timeout=1.0):
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except Exception:
        return False

def dig_test(host):
    rc, out, err = run_cmd(
        ["dig", f"@{host}", DNS_TEST_NAME, "+short", "+time=1", "+tries=1"],
        timeout=3,
    )
    if rc != 0:
        return False, ""
    lines = [x.strip() for x in out.splitlines() if x.strip()]
    return bool(lines), lines[0] if lines else ""

def auth_header():
    if not ADGUARD_USER or not ADGUARD_PASSWORD or ADGUARD_PASSWORD == "CHANGE_ME":
        return None
    raw = f"{ADGUARD_USER}:{ADGUARD_PASSWORD}".encode("utf-8")
    token = base64.b64encode(raw).decode("ascii")
    return {
        "Authorization": f"Basic {token}",
        "Accept": "application/json",
        "User-Agent": "CM4-Dual-DNS-LCD",
    }

def http_json(base_url, path, timeout=3):
    headers = auth_header()
    if headers is None:
        return 0, {"error": "auth not configured"}

    req = urllib.request.Request(base_url.rstrip("/") + path, headers=headers, method="GET")

    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            body = r.read().decode("utf-8", errors="replace")
            return r.status, json.loads(body)
    except urllib.error.HTTPError as e:
        return e.code, {"error": f"HTTP {e.code}"}
    except Exception as e:
        return 0, {"error": str(e)}

def get_adguard_stats(host):
    bases = [
        f"http://{host}",
        f"http://{host}:3000",
    ]

    result = {
        "api": False,
        "running": False,
        "protection": False,
        "queries": 0,
        "blocked": 0,
        "block_pct": 0.0,
        "avg_ms": 0.0,
        "base": "",
        "error": "",
    }

    for base in bases:
        sc, status = http_json(base, "/control/status")
        if sc in (401, 403):
            result["error"] = "auth failed"
            result["base"] = base
            return result

        if sc != 200:
            continue

        stc, stats = http_json(base, "/control/stats")
        if stc != 200:
            result["error"] = f"stats HTTP {stc}"
            result["base"] = base
            return result

        q = int(stats.get("num_dns_queries", 0) or 0)
        b = int(stats.get("num_blocked_filtering", 0) or 0)
        pct = (b * 100.0 / q) if q else 0.0
        avg_ms = float(stats.get("avg_processing_time", 0.0) or 0.0) * 1000.0

        result.update({
            "api": True,
            "running": bool(status.get("running", False)),
            "protection": bool(status.get("protection_enabled", False)),
            "queries": q,
            "blocked": b,
            "block_pct": pct,
            "avg_ms": avg_ms,
            "base": base,
            "error": "",
        })
        return result

    result["error"] = "api unavailable"
    return result

def check_dns(name, host):
    ping = ping_ms(host)
    port53 = tcp_port_open(host, 53)
    dns_ok, answer = dig_test(host)
    ag = get_adguard_stats(host)

    score = 0
    problems = []

    if ping is None:
        score += 35
        problems.append("PING")
    elif ping > 40:
        score += 10
        problems.append("SLOW")

    if not port53:
        score += 30
        problems.append("PORT53")

    if not dns_ok:
        score += 40
        problems.append("DNSFAIL")

    if ag["api"]:
        if not ag["running"]:
            score += 25
            problems.append("AGDOWN")
        if not ag["protection"]:
            score += 20
            problems.append("PROTOFF")
        if ag["avg_ms"] > 80:
            score += 10
            problems.append("AG SLOW")

    if score >= 60:
        state = "BAD"
        color = (255, 40, 30)
    elif score >= 20:
        state = "WARN"
        color = (255, 210, 40)
    else:
        state = "OK"
        color = (40, 255, 100)

    return {
        "name": name,
        "host": host,
        "ping": ping,
        "port53": port53,
        "dns_ok": dns_ok,
        "answer": answer,
        "ag": ag,
        "score": score,
        "state": state,
        "color": color,
        "problems": problems,
    }

def human_count(n):
    n = int(n or 0)
    if n >= 1_000_000:
        return f"{n/1_000_000:.1f}M"
    if n >= 1_000:
        return f"{n/1_000:.1f}K"
    return str(n)

def show(disp, img):
    if LCD_ROTATE in (90, 180, 270):
        img = img.rotate(LCD_ROTATE, expand=True)
    disp.ShowImage(img)

def panel(draw, x, y, w, h, d):
    c = d["color"]
    fill = (0, 0, 0)

    draw.rounded_rectangle((x, y, x+w, y+h), radius=9, fill=fill, outline=c, width=2)

    # Top status band
    draw.rectangle((x+5, y+5, x+w-5, y+35), fill=c)

    state_label = d["state"]
    if state_label == "BAD":
        state_label = "FAIL"

    fg = (0, 0, 0) if d["state"] != "BAD" else (255, 255, 255)

    # Short names now fit cleanly: DNS1 / DNS2
    draw.text((x+10, y+8), d["name"], fill=fg, font=FONT_MED)

    # Status badge with more room
    badge_x1 = x + w - 60
    badge_y1 = y + 8
    badge_x2 = x + w - 9
    badge_y2 = y + 30
    draw.rounded_rectangle((badge_x1, badge_y1, badge_x2, badge_y2), radius=4, fill=(0, 0, 0))
    draw.text((badge_x1 + 5, badge_y1 + 1), state_label, fill=(255, 255, 255), font=FONT_SM)

    # IP address
    draw.text((x+10, y+43), d["host"], fill=(120, 230, 255), font=FONT_SM)

    ping = "--" if d["ping"] is None else f"{d['ping']:.1f}ms"
    draw.text((x+10, y+64), "PING", fill=(255, 255, 255), font=FONT_SM)
    draw.text((x+62, y+64), ping, fill=(255, 255, 80), font=FONT_SM)

    port = "OPEN" if d["port53"] else "FAIL"
    port_color = (90, 255, 120) if d["port53"] else (255, 80, 80)
    draw.text((x+10, y+87), "53", fill=(255, 255, 255), font=FONT_SM)
    draw.text((x+42, y+87), port, fill=port_color, font=FONT_SM)

    dns = "OK" if d["dns_ok"] else "FAIL"
    dns_color = (90, 255, 120) if d["dns_ok"] else (255, 80, 80)
    draw.text((x+94, y+87), dns, fill=dns_color, font=FONT_SM)

    ag = d["ag"]

    # No API wording is drawn. This area shows useful data instead.
    if ag["api"]:
        prot = "ON" if ag["protection"] else "OFF"
        prot_color = (90, 255, 120) if ag["protection"] else (255, 80, 80)

        draw.text((x+10, y+111), "PROT", fill=(255, 255, 255), font=FONT_SM)
        draw.text((x+64, y+111), prot, fill=prot_color, font=FONT_SM)

        draw.text((x+10, y+133), f"BLK {ag['block_pct']:.0f}%", fill=(255, 230, 80), font=FONT_TINY)
        draw.text((x+78, y+133), f"{ag['avg_ms']:.1f}ms", fill=(120, 230, 255), font=FONT_TINY)

        draw.text((x+10, y+150), f"Q {human_count(ag['queries'])}", fill=(120, 230, 255), font=FONT_TINY)
        draw.text((x+78, y+150), f"B {human_count(ag['blocked'])}", fill=(255, 190, 80), font=FONT_TINY)
    else:
        answer = d.get("answer", "") or "--"
        if len(answer) > 13:
            answer = answer[:13]

        draw.text((x+10, y+111), "ANS", fill=(255, 255, 255), font=FONT_SM)
        draw.text((x+52, y+111), answer, fill=(120, 230, 255), font=FONT_TINY)

        score_color = (90, 255, 120) if d["score"] < 20 else (255, 230, 80) if d["score"] < 60 else (255, 80, 80)
        draw.text((x+10, y+133), "SCORE", fill=(255, 255, 255), font=FONT_TINY)
        draw.text((x+70, y+133), str(d["score"]), fill=score_color, font=FONT_TINY)

        if d["problems"]:
            prob = ",".join(d["problems"])[:16]
            draw.text((x+10, y+150), prob, fill=(255, 120, 80), font=FONT_TINY)
        else:
            draw.text((x+10, y+150), "LOOKUP GOOD", fill=(90, 255, 120), font=FONT_TINY)

def draw_main(d1, d2):
    img = Image.new("RGB", (W, H), (0, 0, 0))
    draw = ImageDraw.Draw(img)

    # dark blue-black background
    for y in range(H):
        shade = 3 + int(y * 0.035)
        draw.line((0, y, W, y), fill=(shade, shade + 3, shade + 10))

    draw.rectangle((0, 0, W, 25), fill=(2, 6, 12))
    draw.line((0, 25, W, 25), fill=(45, 110, 170))

    both_good = d1["state"] == "OK" and d2["state"] == "OK"
    title_color = (80, 255, 140) if both_good else (255, 210, 60)

    draw.text((8, 5), "DUAL DNS WATCH", fill=title_color, font=FONT_MED)
    draw.text((228, 6), time.strftime("%H:%M:%S"), fill=(255, 255, 255), font=FONT_SM)

    panel(draw, 8, 36, 146, 171, d1)
    panel(draw, 166, 36, 146, 171, d2)

    # bottom summary
    draw.rectangle((0, 216, W, H), fill=(2, 5, 9))
    draw.line((0, 216, W, 216), fill=(45, 110, 170))

    if both_good:
        msg = "Both DNS servers healthy"
        color = (90, 255, 120)
    elif d1["state"] == "OK" or d2["state"] == "OK":
        msg = "One DNS server has a problem"
        color = (255, 210, 60)
    else:
        msg = "Both DNS servers have problems"
        color = (255, 70, 60)

    draw.text((8, 222), msg, fill=color, font=FONT_SM)

    return img

def splash(disp):
    # Smaller boot/splash text so it fits cleanly.
    splash_title = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 20)
    splash_med = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 13)
    splash_small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 10)

    img = Image.new("RGB", (W, H), (0, 0, 0))
    draw = ImageDraw.Draw(img)

    draw.rectangle((0, 0, W, H), fill=(2, 6, 12))
    draw.text((58, 58), "DUAL DNS", fill=(80, 230, 255), font=splash_title)
    draw.text((52, 92), "DNS1  192.168.1.207", fill=(120, 255, 160), font=splash_med)
    draw.text((52, 116), "DNS2  192.168.1.206", fill=(120, 255, 160), font=splash_med)
    draw.text((52, 152), "ping / port 53 / lookup / stats", fill=(255, 255, 255), font=splash_small)

    show(disp, img)
    time.sleep(1.0)

def main():
    disp = LCD_2inch.LCD_2inch()
    disp.Init()
    disp.clear()
    splash(disp)

    d1 = check_dns(DNS1_NAME, DNS1_HOST)
    d2 = check_dns(DNS2_NAME, DNS2_HOST)
    last_scan = time.time()

    frame_time = 1.0 / max(1.0, TARGET_FPS)

    while True:
        start = time.time()

        if start - last_scan >= SCAN_INTERVAL:
            d1 = check_dns(DNS1_NAME, DNS1_HOST)
            d2 = check_dns(DNS2_NAME, DNS2_HOST)
            last_scan = start

        img = draw_main(d1, d2)
        show(disp, img)

        delay = frame_time - (time.time() - start)
        if delay > 0:
            time.sleep(delay)

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
