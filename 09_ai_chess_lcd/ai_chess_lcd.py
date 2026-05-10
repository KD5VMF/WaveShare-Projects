#!/usr/bin/env python3
import os,sys,time,random,shutil
sys.path.insert(0,'/opt/cm4-lcd-suite/common'); from cm4_lcd_common import *
try: import chess, chess.engine
except Exception as e: print('python chess module missing:',e); sys.exit(1)
SKILL=int(os.environ.get('ENGINE_SKILL','18')); MS=int(os.environ.get('MOVE_TIME_MS','650')); PAUSE=float(os.environ.get('PAUSE_BETWEEN_MOVES','0.75')); MAX=int(os.environ.get('MAX_HALF_MOVES','240')); STOCK=os.environ.get('STOCKFISH_PATH','auto')
SYM={chess.PAWN:'♟',chess.KNIGHT:'♞',chess.BISHOP:'♝',chess.ROOK:'♜',chess.QUEEN:'♛',chess.KING:'♚'}; VAL={chess.PAWN:1,chess.KNIGHT:3,chess.BISHOP:3,chess.ROOK:5,chess.QUEEN:9,chess.KING:0}
def spath(): return STOCK if STOCK!='auto' else (shutil.which('stockfish') or '/usr/games/stockfish')
def mat(b): return sum((1 if p.color else -1)*VAL[p.piece_type] for p in b.piece_map().values())
def draw_chess(d,b,last,ev,cp,g):
    bx,by,s=5,33,22
    for r in range(8):
        for f in range(8):
            sq=chess.square(f,7-r); x=bx+f*s; y=by+r*s; col=(220,222,198) if (r+f)%2==0 else (52,94,130)
            if last and sq==last.from_square: col=(220,180,70)
            if last and sq==last.to_square: col=(255,220,90)
            d.rectangle((x,y,x+s-1,y+s-1),fill=col)
    d.rectangle((bx-1,by-1,bx+176,by+176),outline=(180,220,255)); pf=font(21)
    for sq,p in b.piece_map().items():
        f=chess.square_file(sq); r=7-chess.square_rank(sq); x=bx+f*s; y=by+r*s; sym=SYM[p.piece_type]; fill=(250,250,235) if p.color else (5,8,14); shadow=(0,0,0) if p.color else (240,240,240); d.text((x+4,y-1),sym,fill=shadow,font=pf); d.text((x+3,y-2),sym,fill=fill,font=pf)
    panel(d,(188,31,315,215)); d.text((197,38),'AI vs AI',fill=(80,220,255),font=F_MED); d.text((265,39),f'G{g}',fill=(255,220,110),font=F_SM); d.text((197,62),f'Turn {"WHITE" if b.turn else "BLACK"}',fill=(245,245,235) if b.turn else (160,190,255),font=F_SM); d.text((197,82),f'Move {b.fullmove_number}',fill=(220,220,220),font=F_SM); d.text((197,102),f'Eval {ev}',fill=(245,245,235) if cp>=0 else (140,170,255),font=F_MED); bar(d,197,124,104,9,max(-900,min(900,cp))+900,1800,(230,230,220) if cp>=0 else (80,120,255)); st='CHECKMATE' if b.is_checkmate() else 'STALEMATE' if b.is_stalemate() else 'CHECK' if b.is_check() else 'PLAYING'; d.text((197,142),st,fill=(255,90,80) if st!='PLAYING' else (100,255,140),font=F_MED); d.text((197,165),f'Mat {mat(b):+d}',fill=(255,220,120),font=F_SM); 
    if last: d.text((197,184),last.uci(),fill=(255,255,255),font=F_MED)
def main():
    disp=init_lcd(); splash(disp,'AI Chess','Stockfish vs Stockfish','self-playing smart chess'); eng=None; err=''
    try:
        eng=chess.engine.SimpleEngine.popen_uci(spath()); cfg={}
        if 'Skill Level' in eng.options: cfg['Skill Level']=max(0,min(20,SKILL))
        if 'Threads' in eng.options: cfg['Threads']=1
        if 'Hash' in eng.options: cfg['Hash']=64
        if cfg: eng.configure(cfg)
    except Exception as e: err=str(e)[:60]
    game=0
    while True:
        game+=1; b=chess.Board(); last=None; ev='0.00'; cp=0
        while not b.is_game_over(claim_draw=True) and len(b.move_stack)<MAX:
            im=img(); d=ImageDraw.Draw(im); header(d,'AI Chess','Stockfish' if eng else 'Fallback',eng is not None); draw_chess(d,b,last,ev,cp,game); d.text((6,226),f'Skill {SKILL}/20 {MS}ms/move' if eng else 'Stockfish failed: '+err,fill=(160,160,160),font=F_TINY); show(disp,im)
            if eng:
                try:
                    info=eng.analyse(b,chess.engine.Limit(time=MS/1000),multipv=3); lines=info if isinstance(info,list) else [info]; cand=[]
                    for inf in lines:
                        if inf.get('pv'):
                            score=inf['score']; side=score.pov(b.turn).score(mate_score=100000) or 0; white=score.white().score(mate_score=100000) or 0; cand.append((side,white,inf['pv'][0]))
                    cand.sort(reverse=True,key=lambda x:x[0]); c=random.choice(cand[:min(2,len(cand))]); cp=c[1]; ev=f'{cp/100:+.2f}' if abs(cp)<90000 else 'MATE'; mv=c[2]
                except Exception: mv=random.choice(list(b.legal_moves))
            else: mv=random.choice(list(b.legal_moves))
            last=mv; b.push(mv); time.sleep(PAUSE)
        im=img(); d=ImageDraw.Draw(im); header(d,'AI Chess',b.result(claim_draw=True),False); draw_chess(d,b,last,ev,cp,game); d.text((6,226),'Game over. Restarting soon.',fill=(255,220,90),font=F_TINY); show(disp,im); time.sleep(4)
main()
