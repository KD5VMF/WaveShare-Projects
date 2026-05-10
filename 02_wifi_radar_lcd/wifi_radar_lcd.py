#!/usr/bin/env python3
import os,re,time,math,hashlib,sys
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
IF=os.environ.get('WIFI_IFACE','wlan0'); SCAN=int(os.environ.get('SCAN_INTERVAL','15')); TITLE=os.environ.get('RADAR_TITLE','WiFi Radar')
def scan():
    rc,out,err=run(['iwlist',IF,'scan'],18); aps=[]; cur=None
    if rc: rc,out,err=run(['iw','dev',IF,'scan'],18)
    for line in out.splitlines():
        s=line.strip()
        if s.startswith('Cell ') or s.startswith('BSS '):
            if cur: aps.append(cur)
            m=re.search(r'([0-9A-Fa-f:]{17})',s); cur={'ssid':'<hidden>','bssid':m.group(1) if m else str(len(aps)),'sig':-100,'sec':'SEC'}
        elif cur:
            m=re.search(r'ESSID:"(.*)"',s)
            if m: cur['ssid']=m.group(1) or '<hidden>'
            if s.startswith('SSID:'): cur['ssid']=s[5:].strip() or '<hidden>'
            m=re.search(r'Signal level=(-?\d+)',s) or re.search(r'signal:\s*(-?\d+)',s)
            if m: cur['sig']=int(m.group(1))
            if 'Encryption key:off' in s: cur['sec']='OPEN'
            if 'RSN:' in s or 'WPA2' in s: cur['sec']='WPA2'
    if cur: aps.append(cur)
    return sorted(aps,key=lambda a:a['sig'],reverse=True)[:18]
def ang(k): return int(hashlib.md5(k.encode()).hexdigest()[:8],16)/0xffffffff*math.tau
def main():
    disp=init_lcd(); splash(disp,'WiFi Radar','Nearby network scanner',IF); aps=[]; last=0
    while True:
        if time.time()-last>SCAN: aps=scan(); last=time.time()
        im=img(); d=ImageDraw.Draw(im); header(d,TITLE,f'{len(aps)} AP',True); cx,cy,r=95,132,82
        for rr in (r,55,28): d.ellipse((cx-rr,cy-rr,cx+rr,cy+rr),outline=(20,120,60))
        swp=(time.time()*1.8)%math.tau
        for i in range(8):
            a=swp-i*.05; d.line((cx,cy,cx+int(math.cos(a)*r),cy+int(math.sin(a)*r)),fill=(0,max(30,170-i*18),0))
        for ap in aps:
            a=ang(ap['bssid']); rr=82-int(max(0,min(1,(ap['sig']+95)/70))*68); x=cx+int(math.cos(a)*rr); y=cy+int(math.sin(a)*rr); color=(255,160,70) if ap['sec']=='OPEN' else (80,210,255)
            d.ellipse((x-4,y-4,x+4,y+4),fill=color)
        panel(d,(178,28,316,222)); d.text((188,34),'TOP NETWORKS',fill=(255,255,255),font=F_MED); y=55
        for i,ap in enumerate(aps[:8],1): d.text((185,y),f'{i}. {ap["ssid"][:12]} {ap["sig"]}',fill=(220,220,220),font=F_TINY); y+=20
        d.text((6,225),'Real scan; angle is decorative.',fill=(150,150,150),font=F_TINY); show(disp,im); time.sleep(.15)
main()
