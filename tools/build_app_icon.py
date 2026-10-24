from pathlib import Path
from PIL import Image
from collections import deque
root=Path(__file__).resolve().parents[1]
# Extract the existing app-badge variant from the supplied brand sheet.
# Preserve its artwork; only remove the connected white exterior when packaging as an icon.
source=Image.open(root/'assets/nhip-brand.png').convert('RGBA')
badge=source.crop((1102,729,1330,950))
pixels=badge.load();w,h=badge.size
queue=deque((x,y) for x in range(w) for y in (0,h-1))
queue.extend((x,y) for y in range(h) for x in (0,w-1))
seen=set()
while queue:
    x,y=queue.popleft()
    if (x,y) in seen: continue
    seen.add((x,y))
    r,g,b,a=pixels[x,y]
    if min(r,g,b)<225:continue
    pixels[x,y]=(r,g,b,0)
    for nx,ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
        if 0<=nx<w and 0<=ny<h:queue.append((nx,ny))
icon=Image.new('RGBA',(256,256),(0,0,0,0))
badge.thumbnail((248,248),Image.Resampling.LANCZOS)
icon.alpha_composite(badge,((256-badge.width)//2,(256-badge.height)//2))
icon.save(root/'assets/nhip-app.png')
icon.save(root/'assets/nhip.ico',format='ICO',sizes=[(16,16),(20,20),(24,24),(32,32),(40,40),(48,48),(64,64),(96,96),(128,128),(256,256)])
print('Created nhip.ico sizes:', Image.open(root/'assets/nhip.ico').ico.sizes())
