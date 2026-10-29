import sys, math
from datetime import datetime, timedelta
sys.argv=['x','56.5','6.0']
src=open('sun.py').read().split('import sys')[0]
exec(src)
for lat,lon in [(56.5,6.0),(56.0,6.5),(55.5,6.5),(57.0,5.5)]:
    t=datetime(1916,5,31,19,30); prev=None; out=[]
    mn=(0,None)
    while t<datetime(1916,6,1,4,0):
        J=jd(t); ra,de=sun(J); a,az=alt(ra,de,J,lat,lon)
        if a<mn[0]: mn=(a,t,az)
        if prev is not None:
            for thr,name in [(-0.833,'sunset/rise'),(-6,'civil'),(-12,'nautical')]:
                if (prev-thr)*(a-thr)<0: out.append((name,t.strftime('%H:%M')))
        prev=a; t+=timedelta(minutes=1)
    print(lat,lon,out,'min %.2f at %s az %.0f'%(mn[0],mn[1].strftime('%H:%M'),mn[2]))
# sun alt per hour table 21..03 at 56.3,6.2
for h in [(5,31,21),(5,31,21,30),(5,31,22),(5,31,22,30),(5,31,23),(5,31,23,30),(6,1,0),(6,1,0,30),(6,1,1),(6,1,1,30),(6,1,2),(6,1,2,30),(6,1,3)]:
    t=datetime(1916,h[0],h[1],h[2],h[3] if len(h)>3 else 0)
    J=jd(t); ra,de=sun(J); a,az=alt(ra,de,J,56.3,6.2)
    print(t.strftime('%d %H:%M'),'%.1f'%a,'%.0f'%az)
