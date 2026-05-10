#!/usr/bin/env python3
import os,re,time,sys
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
HOST=os.environ.get('PFSENSE_HOST','192.168.1.1'); COMM=os.environ.get('SNMP_COMMUNITY','CHANGE_ME'); IF=os.environ.get('PFSENSE_IFACE','re0'); TITLE=os.environ.get('LCD_TITLE','pfSense NetWatch')
OIDN='.1.3.6.1.2.1.31.1.1.1.1'; OIDO='.1.3.6.1.2.1.2.2.1.8'; OI='.1.3.6.1.2.1.31.1.1.1.6'; OO='.1.3.6.1.2.1.31.1.1.1.10'
def sg(oid):
    if COMM=='CHANGE_ME': return ''
    rc,o,e=run(['snmpget','-v2c','-c',COMM,'-On','-Oqv','-t','1','-r','0',HOST,oid],2); return o if rc==0 else ''
def sw(oid):
    if COMM=='CHANGE_ME': return ''
    rc,o,e=run(['snmpwalk','-v2c','-c',COMM,'-On','-t','1','-r','0',HOST,oid],4); return o if rc==0 else ''
def disc():
    for line in sw(OIDN).splitlines():
        if '=' not in line: continue
        l,r=line.split('=',1); name=r.split(':')[-1].strip().strip('"'); m=re.search(r'\.(\d+)\s*$',l)
        if m and (name==IF or IF.lower() in name.lower()): return m.group(1),name
    return None,IF
def main():
    disp=init_lcd(); splash(disp,'pfSense','NetWatch dashboard',HOST); idx,name=disc(); li=lo=lt=None; rx=tx=0; peak=1
    while True:
        probs=[]; pf=ping_ms(HOST); wan=ping_ms(os.environ.get('WAN_TEST_HOST','1.1.1.1')); dok=dns_ok(os.environ.get('DNS_TEST_NAME','google.com'))
        if pf is None: probs.append('pfSense ping failed')
        if wan is None: probs.append('Internet ping failed')
        if not dok: probs.append('DNS lookup failed')
        oper='?'
        if idx:
            i=to_int(sg(f'{OI}.{idx}')); o=to_int(sg(f'{OO}.{idx}')); op=to_int(sg(f'{OIDO}.{idx}')); oper='UP' if op==1 else 'DOWN'
            t=time.time()
            if None not in (i,o,li,lo,lt):
                dt=max(.25,t-lt); rx=max(0,(i-li)*8/dt); tx=max(0,(o-lo)*8/dt); peak=max(peak*.995,rx+tx,1)
            li,lo,lt=i,o,t
        else: probs.append(f'No SNMP iface {IF}')
        im=img(); d=ImageDraw.Draw(im); ok=not probs; header(d,TITLE,'OK' if ok else 'ALERT',ok)
        d.text((8,34),f'{name} {oper}',fill=(90,255,120) if oper=='UP' else (255,80,80),font=F_MED)
        d.text((8,61),'RX',fill=(80,200,255),font=F_MED); d.text((55,55),bps(rx),fill=(255,255,255),font=F_BIG)
        d.text((8,103),'TX',fill=(255,190,80),font=F_MED); d.text((55,97),bps(tx),fill=(255,255,255),font=F_BIG)
        bar(d,8,158,300,15,rx,peak,(80,200,255),'RX live scale'); bar(d,8,198,300,15,tx,peak,(255,170,60),'TX live scale')
        d.text((8,220),f'pf {"--" if pf is None else str(round(pf,1))+"ms"} net {"--" if wan is None else str(round(wan,1))+"ms"} DNS {"OK" if dok else "FAIL"}',fill=(180,180,180),font=F_TINY)
        if probs: d.text((8,136),probs[0][:40],fill=(255,90,80),font=F_SM)
        show(disp,im); time.sleep(1)
main()
