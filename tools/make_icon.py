from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[1]/"src/MeowTools/Assets"
size=1024; k=4
im=Image.new("RGBA",(size,size)); mask=Image.new("L",(size,size)); ImageDraw.Draw(mask).rounded_rectangle((16*k,16*k,240*k,240*k),radius=52*k,fill=255)
gradient=Image.new("RGBA",(size,size)); draw=ImageDraw.Draw(gradient)
for y in range(size):
 t=y/(size-1); color=tuple(round(a*(1-t)+b*t) for a,b in zip((166,134,219),(105,78,172)))+(255,); draw.line((0,y,size,y),fill=color)
im.paste(gradient,(0,0),mask); d=ImageDraw.Draw(im)
def polygon(points,fill): d.polygon([(x*k,y*k) for x,y in points],fill=fill)
cream=(255,250,247); pink=(238,186,206); ink=(110,76,158)
polygon([(61,115),(60,54),(111,88)],cream); polygon([(145,88),(196,54),(195,115)],cream)
polygon([(71,95),(70,70),(94,89)],pink); polygon([(162,89),(186,70),(185,95)],pink)
d.ellipse((48*k,77*k,208*k,205*k),fill=cream)
for x in [93,163]: d.ellipse(((x-5)*k,125*k,(x+5)*k,140*k),fill=ink)
polygon([(122,147),(134,147),(128,154)],pink)
d.arc((115*k,147*k,128*k,165*k),0,180,fill=ink,width=3*k); d.arc((128*k,147*k,141*k,165*k),0,180,fill=ink,width=3*k)
for x0,x1 in [(65,85),(171,191)]:
 for y in [148,158]: d.line((x0*k,y*k,x1*k,(y-3)*k),fill=pink,width=3*k)
icon=im.resize((256,256),Image.Resampling.LANCZOS); root.mkdir(parents=True,exist_ok=True)
icon.save(root/'app.png'); icon.save(root/'app.ico',format='ICO',sizes=[(n,n) for n in (16,20,24,32,40,48,64,128,256)])
print('Created original cat icon in nine sizes.')
