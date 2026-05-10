#!/usr/bin/env python3
import os,sys,time,json,shlex
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
PF=os.environ.get('PFSENSE_HOST','192.168.1.1'); WAN=os.environ.get('WAN_TEST_HOST','1.1.1.1'); DNS=os.environ.get('DNS_TEST_NAME','google.com'); TPOT=os.environ.get('TPOT_HOST','192.168.1.80')
def temp():
    try: return int(open('/sys/class/thermal/thermal_zone0/temp').read())/1000
    except Exception: return None
def hits():
    key=os.environ.get('TPOT_KEY','/home/sysop/.ssh/tpot_lcd_ed25519'); user=os.environ.get('TPOT_USER','sysop'); port=os.environ.get('TPOT_PORT','64295'); es=os.environ.get('TPOT_ES_URL','http://127.0.0.1:64298')
    script=f"ES={shlex.quote(es)}; curl -s --max-time 4 -H 'Content-Type: application/json' -XPOST \"$ES/_count\" --data-binary '{{\"query\":{{\"range\":{{\"@timestamp\":{{\"gte\":\"now-15m\"}}}}}}}}'"
    rc,out,err=ssh_remote(user,TPOT,port,key,script,8)
    try: return int(json.loads(out).get('count',0)) if rc==0 else None
    except Exception: return 0
def main():
    disp=init_lcd(); splash(disp,'Cyber Threat','Home lab alert meter','pfSense / T-Pot / DNS')
    while True:
        score=0; why=[]; pf=ping_ms(PF); wan=ping_ms(WAN); dok=dns_ok(DNS); h=hits(); t=temp()
        if pf is None: score+=45; why.append('pfSense unreachable')
        if wan is None: score+=35; why.append('Internet ping failed')
        elif wan>100: score+=15; why.append(f'Internet slow {wan:.0f}ms')
        if not dok: score+=35; why.append('DNS lookup failed')
        if h is None: score+=15; why.append('T-Pot check failed')
        elif h>250: score+=30; why.append(f'T-Pot burst {human(h)}/15m')
        elif h>25: score+=12; why.append(f'T-Pot active {human(h)}/15m')
        if t and t>75: score+=12; why.append(f'CM4 warm {t:.0f}C')
        score=min(100,score)
        if score>=76: lvl,lab,c,fg='RED','HIGH ALERT',(255,35,25),(255,255,255)
        elif score>=46: lvl,lab,c,fg='ORANGE','ELEVATED',(255,120,20),(0,0,0)
        elif score>=21: lvl,lab,c,fg='YELLOW','WATCH',(255,220,35),(0,0,0)
        else: lvl,lab,c,fg='GREEN','NORMAL',(20,210,80),(0,0,0)
        if not why: why=['Network looks normal','Core checks are OK']
        im=img(c); d=ImageDraw.Draw(im); d.text((8,5),'Cyber Threat',fill=fg,font=F_MED); d.text((230,6),time.strftime('%H:%M:%S'),fill=fg,font=F_SM); d.text((16,35),lvl,fill=fg,font=F_HUGE); d.text((18,78),lab,fill=fg,font=F_MED); d.text((230,35),str(score),fill=fg,font=F_HUGE); d.text((260,75),'/100',fill=fg,font=F_SM); bar(d,16,98,288,12,score,100,(0,0,0) if fg!=(0,0,0) else (255,255,255)); panel(d,(8,125,312,230),fill=(0,0,0),outline=(255,255,255)); d.text((16,133),"WHAT'S GOING ON:",fill=(255,255,255),font=F_SM); y=154
        for r in why[:5]: d.text((16,y),'• '+r[:42],fill=(255,255,255),font=F_TINY); y+=14
        d.text((16,214),f'pf {"--" if pf is None else str(round(pf,1))+"ms"} net {"--" if wan is None else str(round(wan,1))+"ms"} DNS {"OK" if dok else "FAIL"} TPot {human(h) if h is not None else "--"}',fill=(200,200,200),font=F_TINY); show(disp,im); time.sleep(5)
main()
