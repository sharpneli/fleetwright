import sys, math
from datetime import datetime, timedelta
src=open('sun.py').read().split('import sys')[0]
exec(src)
def sunlon(J):
    T=(J-2451545)/36525
    L0=(280.46646+36000.76983*T)%360
    M=math.radians(357.52911+35999.05029*T)
    C=(1.914602-0.004817*T)*math.sin(M)+0.019993*math.sin(2*M)
    return (L0+C-0.00569)%360
t=datetime(1916,5,30,12)
while t<datetime(1916,6,2):
    J=jd(t); el=(moon(J)[2]-sunlon(J)+180)%360-180
    if abs(el)<0.3: print('conj approx',t, round(el,2))
    t+=timedelta(minutes=10)
lat,lon=56.5,6.0
t=datetime(1916,5,31,19); prev=None
while t<datetime(1916,6,1,4):
    J=jd(t); mra,mde,_=moon(J); a,az=alt(mra,mde,J,lat,lon); a_topo=a-0.95*math.cos(math.radians(a))
    h=a_topo+0.57  # refraction ~ at horizon; event when topo alt = -0.57 (approx incl semidiam 0.26 -> -0.83+0.26?)
    if prev is not None and (prev+0.83-0.26-0)*(a_topo+0.83-0.26)<0: pass
    if prev is not None and (prev-(-0.57))*(a_topo-(-0.57))<0: print('moon event',t.strftime('%H:%M'),'az %.0f'%az)
    prev=a_topo; t+=timedelta(minutes=1)
