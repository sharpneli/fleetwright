import math
from datetime import datetime, timedelta
def jd(dt):
    y,m=dt.year,dt.month
    d=dt.day+(dt.hour+dt.minute/60)/24
    if m<=2: y-=1; m+=12
    A=y//100; B=2-A+A//4
    return int(365.25*(y+4716))+int(30.6001*(m+1))+d+B-1524.5
def gmst(J):
    T=(J-2451545)/36525
    return (280.46061837+360.98564736629*(J-2451545)+0.000387933*T*T)%360
def sun(J):
    T=(J-2451545)/36525
    L0=(280.46646+36000.76983*T)%360
    M=math.radians(357.52911+35999.05029*T)
    C=(1.914602-0.004817*T)*math.sin(M)+0.019993*math.sin(2*M)+0.000289*math.sin(3*M)
    lam=math.radians(L0+C-0.00569)
    eps=math.radians(23.439291-0.0130042*T)
    ra=math.atan2(math.cos(eps)*math.sin(lam),math.cos(lam)); dec=math.asin(math.sin(eps)*math.sin(lam))
    return math.degrees(ra)%360, math.degrees(dec)
def moon(J):
    d=J-2451543.5
    N=math.radians((125.1228-0.0529538083*d)%360); i=math.radians(5.1454); w=math.radians((318.0634+0.1643573223*d)%360)
    a=60.2666; e=0.0549; M=math.radians((115.3654+13.0649929509*d)%360)
    E=M+e*math.sin(M)*(1+e*math.cos(M))
    for _ in range(5): E=E-(E-e*math.sin(E)-M)/(1-e*math.cos(E))
    xv=a*(math.cos(E)-e); yv=a*math.sqrt(1-e*e)*math.sin(E)
    v=math.atan2(yv,xv); r=math.hypot(xv,yv)
    xh=r*(math.cos(N)*math.cos(v+w)-math.sin(N)*math.sin(v+w)*math.cos(i))
    yh=r*(math.sin(N)*math.cos(v+w)+math.cos(N)*math.sin(v+w)*math.cos(i))
    zh=r*(math.sin(v+w)*math.sin(i))
    lon=math.atan2(yh,xh); lat=math.atan2(zh,math.hypot(xh,yh))
    # perturbations
    Ms=math.radians((356.0470+0.9856002585*d)%360); Ls=Ms+math.radians(282.9404+4.70935e-5*d)
    Lm=M+w+N; D=Lm-Ls; F=Lm-N
    dl=(-1.274*math.sin(M-2*D)+0.658*math.sin(2*D)-0.186*math.sin(Ms)-0.059*math.sin(2*M-2*D)-0.057*math.sin(M-2*D+Ms)+0.053*math.sin(M+2*D)+0.046*math.sin(2*D-Ms)+0.041*math.sin(M-Ms)-0.035*math.sin(D)-0.031*math.sin(M+Ms)-0.015*math.sin(2*F-2*D)+0.011*math.sin(M-4*D))
    db=(-0.173*math.sin(F-2*D)-0.055*math.sin(M-F-2*D)-0.046*math.sin(M+F-2*D)+0.033*math.sin(F+2*D)+0.017*math.sin(2*M+F))
    lon=lon+math.radians(dl); lat=lat+math.radians(db)
    ecl=math.radians(23.4393-3.563e-7*d)
    x=math.cos(lon)*math.cos(lat); y=math.sin(lon)*math.cos(lat); z=math.sin(lat)
    ye=y*math.cos(ecl)-z*math.sin(ecl); ze=y*math.sin(ecl)+z*math.cos(ecl)
    ra=math.degrees(math.atan2(ye,x))%360; dec=math.degrees(math.atan2(ze,math.hypot(x,ye)))
    # elongation
    return ra,dec,math.degrees(lon)%360
def alt(ra,dec,J,lat,lon):
    H=math.radians((gmst(J)+lon-ra)%360)
    la=math.radians(lat); de=math.radians(dec)
    s=math.sin(la)*math.sin(de)+math.cos(la)*math.cos(de)*math.cos(H)
    a=math.asin(s)
    az=math.degrees(math.atan2(-math.sin(H)*math.cos(de),math.cos(la)*math.sin(de)-math.sin(la)*math.cos(de)*math.cos(H)))%360
    return math.degrees(a),az
import sys
lat,lon=float(sys.argv[1]),float(sys.argv[2])
t=datetime(1916,5,31,19,0)
while t<=datetime(1916,6,1,4,0):
    J=jd(t); ra,de=sun(J); a,az=alt(ra,de,J,lat,lon)
    mra,mde,mlon=moon(J); ma,maz=alt(mra,mde,J,lat,lon)
    # sun ecliptic lon approx
    T=(J-2451545)/36525
    print(t.strftime('%d %H:%M GMT'), 'sun alt %6.2f az %5.1f'%(a,az),' moon alt(geoc) %6.2f az %5.1f'%(ma,maz))
    t+=timedelta(minutes=30)
# moon phase: elongation
for t in [datetime(1916,5,31,22),datetime(1916,6,1,2)]:
    J=jd(t)
    T=(J-2451545)/36525
    L0=(280.46646+36000.76983*T)%360
    M=math.radians(357.52911+35999.05029*T)
    C=1.914602*math.sin(M)+0.019993*math.sin(2*M)
    sl=(L0+C)%360
    mra,mde,mlon=moon(J)
    el=(mlon-sl)%360
    illum=(1-math.cos(math.radians(el)))/2
    print(t,'moon-sun elong',round(el,1),'illum',round(illum,3))
