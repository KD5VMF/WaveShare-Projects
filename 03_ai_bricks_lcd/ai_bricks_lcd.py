#!/usr/bin/env python3
import os,sys,time,random,math
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
def main():
    disp=init_lcd(); splash(disp,'AI Bricks','Self-playing arcade','dynamic paddle AI'); level=1; score=0
    def make():
        return [[8+c*31,35+r*15,28,12,[(255,70,70),(255,160,50),(255,220,70),(80,230,100),(80,190,255)][r%5]] for r in range(min(7,4+level//2)) for c in range(10)]
    bricks=make(); px=130; pw=58; py=218; bx=160; by=170; vx=2.8; vy=-3.2; trail=[]
    while True:
        tx,ty,tvx,tvy=bx,by,vx,vy
        for _ in range(200):
            if tvy<=0: break
            tx+=tvx; ty+=tvy
            if tx<4 or tx>316: tvx=-tvx
            if ty>=py: break
        px+=max(-6,min(6,tx-pw/2-px)); px=max(2,min(320-pw-2,px)); vx+=random.uniform(-.015,.015); bx+=vx; by+=vy; trail=(trail+[(bx,by)])[-12:]
        if bx<4 or bx>316: vx=-vx
        if by<28: vy=abs(vy); vx+=random.uniform(-.4,.4)
        if vy>0 and py-4<=by<=py+10 and px<=bx<=px+pw:
            hit=(bx-(px+pw/2))/(pw/2); sp=min(6.5,math.hypot(vx,vy)+.15); vx=hit*sp; vy=-math.sqrt(max(1,sp*sp-vx*vx))
        if by>240: bx,by=160,170; vx=random.choice([-1,1])*3; vy=-3.4
        for b in list(bricks):
            if b[0]-4<=bx<=b[0]+b[2]+4 and b[1]-4<=by<=b[1]+b[3]+4: bricks.remove(b); score+=10; vy=-vy; vx+=random.uniform(-.6,.6); break
        if not bricks: level+=1; bricks=make(); bx,by=160,170
        im=img((3,5,12)); d=ImageDraw.Draw(im); header(d,'AI Bricks',f'LV {level}',True)
        for b in bricks: d.rounded_rectangle((b[0],b[1],b[0]+b[2],b[1]+b[3]),radius=3,fill=b[4],outline=(30,30,40))
        for i,(x,y) in enumerate(trail): f=(i+1)/len(trail); d.ellipse((x-3*f,y-3*f,x+3*f,y+3*f),fill=(int(80*f),int(180*f),255))
        d.rounded_rectangle((px,py,px+pw,py+8),radius=4,fill=(70,180,255),outline=(220,250,255)); d.ellipse((bx-4,by-4,bx+4,by+4),fill=(255,255,255))
        d.text((6,226),f'score {score} bricks {len(bricks)}',fill=(170,170,170),font=F_TINY); show(disp,im); time.sleep(1/30)
main()
