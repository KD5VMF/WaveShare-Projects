#!/usr/bin/env python3
import os,sys,time,re
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
URLS=[x.strip().rstrip('/') for x in os.environ.get('ADGUARD_URLS','http://192.168.1.206,http://192.168.1.206:3000,http://192.168.1.207,http://192.168.1.207:3000').split(',') if x.strip()]; USER=os.environ.get('ADGUARD_USER','admin'); PASS=os.environ.get('ADGUARD_PASSWORD','CHANGE_ME')
def host(u):
    m=re.match(r'https?://([^/:]+)',u); return m.group(1) if m else u
def fetch():
    d={'ok':False,'err':'not reachable','host':'','q':0,'b':0,'pct':0,'run':False,'prot':False}
    if PASS=='CHANGE_ME': d['err']='set ADGUARD_PASSWORD'; return d
    for u in URLS:
        sc,st=http_json(u+'/control/status',basic_auth(USER,PASS));
        if sc in (401,403): d.update(err='auth failed',host=host(u)); return d
        if sc!=200: continue
        sc2,stats=http_json(u+'/control/stats',basic_auth(USER,PASS));
        if sc2!=200: continue
        q=int(stats.get('num_dns_queries',0) or 0); b=int(stats.get('num_blocked_filtering',0) or 0); d.update(ok=True,host=host(u),q=q,b=b,pct=b*100/q if q else 0,run=bool(st.get('running')),prot=bool(st.get('protection_enabled')),err=''); return d
    return d
def main():
    disp=init_lcd(); splash(disp,'AdGuard LCD','DNS protection monitor','queries / blocks'); data=None; last=0
    while True:
        if data is None or time.time()-last>10: data=fetch(); last=time.time()
        ok=data['ok'] and data['run'] and data['prot']; im=img(); d=ImageDraw.Draw(im); header(d,'AdGuard LCD','OK' if ok else 'ALERT',ok)
        c=(20,210,80) if ok else (255,80,60); d.polygon([(68,40),(120,60),(108,140),(68,180),(28,140),(16,60)],fill=(5,35,20) if ok else (40,5,5),outline=c)
        d.text((38,78),'BLOCK',fill=(255,255,255),font=F_SM); d.text((33,100),f'{data["pct"]:.1f}%',fill=(255,220,80),font=F_BIG)
        d.text((142,45),'ADGUARD HOME',fill=(255,255,255),font=F_MED); d.text((142,70),data['host'] or data['err'],fill=(190,220,255),font=F_SM); d.text((142,100),f'RUN {"YES" if data["run"] else "NO"}',fill=c,font=F_MED); d.text((142,130),f'PROT {"ON" if data["prot"] else "OFF"}',fill=c,font=F_MED)
        panel(d,(132,160,312,218)); d.text((142,168),f'Queries {human(data["q"])}',fill=(120,220,255),font=F_MED); d.text((142,194),f'Blocked {human(data["b"])}',fill=(255,190,80),font=F_MED)
        show(disp,im); time.sleep(.2)
main()
