#!/usr/bin/env python3
import os,sys,time,json,shlex
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
HOST=os.environ.get('TPOT_HOST','192.168.1.80'); USER=os.environ.get('TPOT_USER','sysop'); PORT=os.environ.get('TPOT_PORT','64295'); KEY=os.environ.get('TPOT_KEY','/home/sysop/.ssh/tpot_lcd_ed25519'); ES=os.environ.get('TPOT_ES_URL','http://127.0.0.1:64298')
def fetch():
    script='\n'.join([f'ES={shlex.quote(ES)}','echo HOST=$(hostname)','echo HEALTH=$(curl -s --max-time 4 "$ES/_cluster/health" | tr "\\n" " ")','echo C15=$(curl -s --max-time 5 -H "Content-Type: application/json" -XPOST "$ES/_count" --data-binary \'{"query":{"range":{"@timestamp":{"gte":"now-15m"}}}}\' | tr "\\n" " ")','echo C24=$(curl -s --max-time 5 -H "Content-Type: application/json" -XPOST "$ES/_count" --data-binary \'{"query":{"range":{"@timestamp":{"gte":"now-24h"}}}}\' | tr "\\n" " ")','echo DOCKER=$(docker ps -q 2>/dev/null | wc -l)'])
    rc,out,err=ssh_remote(USER,HOST,PORT,KEY,script,18); d={'ssh':rc==0,'err':err,'es':'unknown','c15':0,'c24':0,'docker':0}
    for line in out.splitlines():
        if line.startswith('HEALTH=') and '{' in line: d['es']=json.loads(line.split('=',1)[1]).get('status','unknown')
        elif line.startswith('C15=') and '{' in line: d['c15']=json.loads(line.split('=',1)[1]).get('count',0)
        elif line.startswith('C24=') and '{' in line: d['c24']=json.loads(line.split('=',1)[1]).get('count',0)
        elif line.startswith('DOCKER='): d['docker']=to_int(line) or 0
    return d
def main():
    disp=init_lcd(); splash(disp,'T-Pot LCD','Tar-pit attack monitor',HOST); data=None; last=0
    while True:
        if data is None or time.time()-last>20: data=fetch(); last=time.time()
        ok=data['ssh'] and data['es'] in ('green','yellow'); im=img(); d=ImageDraw.Draw(im); header(d,'T-Pot LCD','OK' if ok else 'ALERT',ok)
        d.ellipse((20,60,130,170),outline=(35,150,80)); swp=(time.time()*2)%math.tau; d.line((75,115,75+int(math.cos(swp)*55),115+int(math.sin(swp)*55)),fill=(0,180,0))
        d.text((150,45),f'SSH {"OK" if data["ssh"] else "FAIL"}',fill=(90,255,120) if data['ssh'] else (255,80,80),font=F_MED); d.text((150,75),f'Elastic {data["es"].upper()}',fill=(255,220,80),font=F_MED)
        d.text((150,110),f'15m {human(data["c15"])}',fill=(255,170,70),font=F_BIG); d.text((150,150),f'24h {human(data["c24"])}',fill=(255,220,120),font=F_MED); d.text((150,180),f'Docker {data["docker"]}',fill=(180,180,180),font=F_SM)
        if not data['ssh']: d.text((8,225),data['err'][:50],fill=(255,100,80),font=F_TINY)
        show(disp,im); time.sleep(.2)
main()
