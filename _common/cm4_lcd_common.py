#!/usr/bin/env python3
import os, re, sys, time, math, json, base64, socket, subprocess, urllib.request, urllib.error
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
W,H=320,240
LCD_LIB_DIR=os.environ.get('LCD_LIB_DIR','/home/sysop/CM4-NAS-Double-Deck_Demo/RaspberryPi')
LCD_ROTATE=int(os.environ.get('LCD_ROTATE','180'))
FONT='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'; BOLD='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
def font(sz,b=False): return ImageFont.truetype(BOLD if b else FONT, sz)
F_HUGE=font(38,1); F_BIG=font(26,1); F_MED=font(15,1); F_SM=font(12); F_TINY=font(9); F_MICRO=font(8)
def init_lcd():
    sys.path.insert(0,LCD_LIB_DIR); from lib import LCD_2inch
    d=LCD_2inch.LCD_2inch(); d.Init()
    try: d.clear()
    except Exception: pass
    return d
def show(disp,img):
    rot=int(os.environ.get('LCD_ROTATE',str(LCD_ROTATE)))
    if rot in (90,180,270): img=img.rotate(rot,expand=True)
    disp.ShowImage(img)
def run(cmd,timeout=5):
    try:
        p=subprocess.run(cmd,text=True,capture_output=True,timeout=timeout); return p.returncode,p.stdout.strip(),p.stderr.strip()
    except Exception as e: return 999,'',str(e)
def ping_ms(host,timeout=1):
    rc,out,err=run(['ping','-c','1','-W',str(timeout),host],timeout+1)
    if rc: return None
    m=re.search(r'time=([\d.]+)',out); return float(m.group(1)) if m else 0.0
def dns_ok(name='google.com'):
    try: socket.gethostbyname(name); return True
    except Exception: return False
def human(n):
    try: n=int(n)
    except Exception: return '--'
    if n>=1000000: return f'{n/1000000:.1f}M'
    if n>=1000: return f'{n/1000:.1f}K'
    return str(n)
def bps(v):
    try: v=float(v)
    except Exception: return '--'
    if v>=1e9: return f'{v/1e9:.2f}G'
    if v>=1e6: return f'{v/1e6:.0f}M'
    if v>=1e3: return f'{v/1e3:.0f}K'
    return f'{v:.0f}b'
def to_int(s):
    m=re.search(r'(\d+)',str(s or '')); return int(m.group(1)) if m else None
def img(bg=(0,0,0)): return Image.new('RGB',(W,H),bg)
def header(d,title,status='',ok=True):
    d.rectangle((0,0,W,24),fill=(3,8,14)); d.line((0,24,W,24),fill=(40,90,150))
    d.text((5,4),title[:20],fill=(80,220,255),font=F_MED)
    if status: d.text((185,5),status[:12],fill=(90,255,120) if ok else (255,80,80),font=F_SM)
    d.text((260,5),time.strftime('%H:%M:%S'),fill=(220,220,220),font=F_TINY)
def panel(d,box,fill=(5,8,14),outline=(40,90,150)): d.rounded_rectangle(box,radius=7,fill=fill,outline=outline)
def bar(d,x,y,w,h,val,maxv,color,label=''):
    d.rectangle((x,y,x+w,y+h),outline=(70,70,70),fill=(8,8,8)); pct=0 if maxv<=0 else max(0,min(1,float(val)/float(maxv)))
    d.rectangle((x+1,y+1,x+int(w*pct),y+h-1),fill=color)
    if label: d.text((x,y-13),label,fill=(180,180,180),font=F_TINY)
def splash(disp,title,sub='',detail=''):
    im=img((2,5,12)); dr=ImageDraw.Draw(im); dr.text((30,60),title[:20],fill=(80,220,255),font=F_BIG)
    if sub: dr.text((35,102),sub[:32],fill=(255,255,255),font=F_MED)
    if detail: dr.text((35,134),detail[:40],fill=(255,220,100),font=F_SM)
    show(disp,im); time.sleep(1)
def basic_auth(u,p): return {'Authorization':'Basic '+base64.b64encode(f'{u}:{p}'.encode()).decode()}
def http_json(url,headers=None,method='GET',data=None,timeout=5):
    try:
        body=None if data is None else json.dumps(data).encode(); h={'Content-Type':'application/json','User-Agent':'CM4-LCD-Suite'}
        if headers: h.update(headers)
        req=urllib.request.Request(url,data=body,headers=h,method=method)
        with urllib.request.urlopen(req,timeout=timeout) as r:
            txt=r.read().decode('utf-8','replace'); return r.status,json.loads(txt) if txt.strip() else {}
    except urllib.error.HTTPError as e: return e.code,{'error':f'HTTP {e.code}'}
    except Exception as e: return 0,{'error':str(e)}
def ssh_remote(user,host,port,key,script,timeout=15):
    if not Path(key).exists(): return 998,'',f'SSH key missing: {key}'
    cmd=['ssh','-p',str(port),'-i',key,'-o','BatchMode=yes','-o','ConnectTimeout=5','-o','StrictHostKeyChecking=accept-new','-o','UserKnownHostsFile=/root/.ssh/known_hosts',f'{user}@{host}','bash -lc '+repr(script)]
    return run(cmd,timeout)
