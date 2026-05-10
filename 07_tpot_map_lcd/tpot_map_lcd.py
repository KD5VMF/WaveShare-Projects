#!/usr/bin/env python3
import os,sys,time,json,shlex,math,hashlib
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
HOST=os.environ.get('TPOT_HOST','192.168.1.80'); USER=os.environ.get('TPOT_USER','sysop'); PORT=os.environ.get('TPOT_PORT','64295'); KEY=os.environ.get('TPOT_KEY','/home/sysop/.ssh/tpot_lcd_ed25519'); ES=os.environ.get('TPOT_ES_URL','http://127.0.0.1:64298')
COUNTRY={'US':(58,74),'CA':(50,55),'BR':(82,138),'GB':(103,65),'DE':(116,72),'FR':(108,76),'RU':(150,56),'CN':(180,94),'JP':(204,93),'IN':(160,117),'AU':(195,163)}
def fetch():
    payload='{"size":0,"query":{"range":{"@timestamp":{"gte":"now-24h"}}},"aggs":{"c":{"terms":{"field":"source.geo.country_iso_code.keyword","size":8}},"p":{"terms":{"field":"destination.port","size":5}}}}'
    script='\n'.join([f'ES={shlex.quote(ES)}','echo DATA=$(curl -s --max-time 6 -H "Content-Type: application/json" -XPOST "$ES/_search" --data-binary '+shlex.quote(payload)+' | tr "\\n" " ")'])
    rc,out,err=ssh_remote(USER,HOST,PORT,KEY,script,18); d={'ok':rc==0,'err':err,'countries':[],'ports':[]}
    for line in out.splitlines():
        if line.startswith('DATA='):
            js=json.loads(line.split('=',1)[1] or '{}'); ag=js.get('aggregations',{}); d['countries']=ag.get('c',{}).get('buckets',[]); d['ports']=ag.get('p',{}).get('buckets',[])
    return d
def xy(k):
    k=str(k).upper()
    if k in COUNTRY: return COUNTRY[k]
    h=int(hashlib.md5(k.encode()).hexdigest()[:8],16); return 20+h%180,45+(h>>8)%115
def world(d):
    d.rounded_rectangle((4,28,212,174),radius=7,outline=(25,70,110),fill=(2,10,22)); land=(15,70,48); edge=(35,140,80)
    for p in [[(18,58),(36,39),(67,42),(83,60),(76,82),(54,91),(31,83)],[(70,113),(88,123),(91,146),(80,170),(66,161),(61,136)],[(103,62),(136,68),(129,84),(109,82),(98,72)],[(113,86),(143,124),(130,159),(109,153),(101,120)],[(132,64),(168,51),(205,68),(205,101),(180,119),(145,105)],[(181,153),(205,158),(211,171),(193,177),(176,168)]]: d.polygon(p,fill=land,outline=edge)
def main():
    disp=init_lcd(); splash(disp,'T-Pot Map','Abstract attack display',HOST); data=None; last=0
    while True:
        if data is None or time.time()-last>20: data=fetch(); last=time.time()
        im=img(); d=ImageDraw.Draw(im); header(d,'T-Pot World Map','OK' if data['ok'] else 'ALERT',data['ok']); world(d); cx,cy=105,105; sw=(time.time()*1.3)%math.tau; d.line((cx,cy,cx+int(math.cos(sw)*95),cy+int(math.sin(sw)*95)),fill=(0,160,50)); d.ellipse((cx-5,cy-5,cx+5,cy+5),fill=(80,220,255))
        for c in data.get('countries',[])[:12]:
            x,y=xy(c.get('key','?')); sz=3+min(5,int(math.log10(max(1,c.get('doc_count',0)))+1)); d.ellipse((x-sz,y-sz,x+sz,y+sz),fill=(255,160,50)); d.line((x,y,cx,cy),fill=(60,35,20))
        panel(d,(218,28,316,218)); d.text((226,36),'COUNTRIES',fill=(255,255,255),font=F_MED); y=60
        for c in data.get('countries',[])[:5]: d.text((226,y),f'{c.get("key")} {human(c.get("doc_count",0))}',fill=(255,190,90),font=F_TINY); y+=16
        d.text((226,150),'PORTS',fill=(180,180,180),font=F_TINY); y=166
        for p in data.get('ports',[])[:4]: d.text((226,y),f'{p.get("key")} {human(p.get("doc_count",0))}',fill=(120,220,255),font=F_TINY); y+=14
        show(disp,im); time.sleep(.2)
main()
