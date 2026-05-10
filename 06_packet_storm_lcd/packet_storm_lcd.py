#!/usr/bin/env python3
import os,sys,time,random,math,re
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
HOST=os.environ.get('PFSENSE_HOST','192.168.1.1'); COMM=os.environ.get('SNMP_COMMUNITY','CHANGE_ME'); IF=os.environ.get('PFSENSE_IFACE','re0')
OIDN='.1.3.6.1.2.1.31.1.1.1.1'; OI='.1.3.6.1.2.1.31.1.1.1.6'; OO='.1.3.6.1.2.1.31.1.1.1.10'
def sg(oid): rc,o,e=run(['snmpget','-v2c','-c',COMM,'-On','-Oqv','-t','1','-r','0',HOST,oid],2); return o if rc==0 else ''
def sw(oid): rc,o,e=run(['snmpwalk','-v2c','-c',COMM,'-On','-t','1','-r','0',HOST,oid],4); return o if rc==0 else ''
def disc():
    for line in sw(OIDN).splitlines():
        if '=' in line:
            l,r=line.split('=',1); name=r.split(':')[-1].strip().strip('"'); m=re.search(r'\.(\d+)$',l.strip())
            if m and (name==IF or IF.lower() in name.lower()): return m.group(1)
    return None
def main():
    disp=init_lcd(); splash(disp,'Packet Storm','pfSense traffic animation',IF); idx=disc(); li=lo=lt=None; rx=tx=0; scale=1e9; drops=[[random.randrange(320),random.randrange(240),random.uniform(2,6)] for _ in range(60)]
    while True:
        if idx and time.time()%1<.05:
            i=to_int(sg(f'{OI}.{idx}')); o=to_int(sg(f'{OO}.{idx}')); t=time.time()
            if None not in (i,o,li,lo,lt): dt=max(.25,t-lt); rx=(i-li)*8/dt; tx=(o-lo)*8/dt; scale=max(scale*.995,rx+tx,1)
            li,lo,lt=i,o,t
        lvl=max(.02,min(1,(rx+tx)/scale)); im=img((3,5,12)); d=ImageDraw.Draw(im); header(d,'Packet Storm','STORM',True)
        for y in range(25,220): d.line((0,y,320,y),fill=(5,8,20+int(lvl*35)))
        target=int(40+lvl*140)
        while len(drops)<target: drops.append([random.randrange(320),random.randrange(-50,0),random.uniform(2,6)])
        drops=drops[:target+20]
        for dr in drops:
            dr[1]+=dr[2]*(1+lvl*3); dr[0]+=math.sin(dr[1]*.03)*lvl*3
            if dr[1]>240: dr[0]=random.randrange(320); dr[1]=random.randrange(-50,0)
            d.line((dr[0],dr[1],dr[0]-int(lvl*8),dr[1]+8+int(lvl*14)),fill=(40,140+int(lvl*90),255))
        if lvl>.45 and random.random()<lvl*.03:
            x=random.randrange(50,270); y=25; p=[]
            while y<180: p.append((x,y)); x+=random.randrange(-25,26); y+=random.randrange(15,30)
            for a,b in zip(p,p[1:]): d.line((a[0],a[1],b[0],b[1]),fill=(255,255,210),width=2)
        panel(d,(8,145,150,214)); panel(d,(170,145,312,214),outline=(90,70,30)); d.text((18,152),'RX RAIN',fill=(80,200,255),font=F_SM); d.text((18,172),bps(rx),fill=(255,255,255),font=F_BIG); d.text((180,152),'TX FIRE',fill=(255,180,80),font=F_SM); d.text((180,172),bps(tx),fill=(255,255,255),font=F_BIG)
        show(disp,im); time.sleep(1/24)
main()
