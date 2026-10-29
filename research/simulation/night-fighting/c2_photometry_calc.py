import math
import numpy as np
DR=math.pi/180
def atmos(HA):
    X=753.66156
    U=math.sin(HA*DR)
    S=math.asin(X*math.cos(HA*DR)/(X+1.0))
    M=X*(math.cos(S)-U)+math.cos(S)
    return math.exp(-0.21*M)*U+0.0289*math.exp(-0.042*M)*(1.0+(HA+90.0)*U/57.29577951)
print("USNO C171 sun illuminance (lux), no refraction")
for h in [10,5,3,0,-3,-6,-9,-12,-15,-18]:
    print(h, "%.3g"%(133775*atmos(h)))
# Schlyter table interpolation in log
sch={0:759,-2:189,-4:29.9,-6:3.41,-8:0.348,-10:0.0421,-12:0.00806,-14:0.00292,-16:0.00144,-18:0.000645,5:4540,10:11100}
def li(h):
    ks=sorted(sch)
    for a,b in zip(ks,ks[1:]):
        if a<=h<=b:
            t=(h-a)/(b-a); return 10**(math.log10(sch[a])*(1-t)+math.log10(sch[b])*t)
print("Schlyter interp -3:",li(-3)," -9:",li(-9))
# Koomen decoded horizon azimuth profiles Sac Peak (cd/ft2)
Z=np.array([0,22.5,45,90,135,180])
prof={
 5:[1000,600,200,130,160,190],
 0:[150,102,62,37,32,38],
 -3:[20,15,11,4.2,2.4,2.4],
 -6:[0.9,0.68,0.4,0.11,0.078,0.078],
 -9:[0.04,0.028,0.017,0.005,0.0036,0.0036],
 -12:[0.0018,0.0014,0.00076,0.00036,0.00026,0.00028],
 -15:[0.00018,0.00013,0.00008,0.00005,0.00005,0.000058],
}
Eh={5:190,0:40,-3:4,-6:0.125,-9:0.005}  # fc, sky on horizontal (Table III)
from scipy.optimize import curve_fit
c=np.cos(Z*DR)
for H,v in prof.items():
    v=np.array(v,float); lv=np.log(v)
    # model 1: ln L = a + k cos Z
    k1,a1=np.polyfit(c,lv,1)
    rms1=np.sqrt(np.mean((a1+k1*c-lv)**2))
    # model 2: L = F + G exp(k(cosZ-1))
    def m2(cz,F,G,k): return F+G*np.exp(k*(cz-1))
    try:
        p,_=curve_fit(lambda cz,lF,lG,k: np.log(m2(cz,np.exp(lF),np.exp(lG),k)), c, lv, p0=[np.log(v[-1]),np.log(v[0]),3],maxfev=20000)
        F,G,k2=np.exp(p[0]),np.exp(p[1]),p[2]
        rms2=np.sqrt(np.mean((np.log(m2(c,F,G,k2))-lv)**2))
    except Exception as e:
        F=G=k2=rms2=float('nan')
    # azimuth mean (trapezoid over 0..180)
    zz=np.linspace(0,180,181); vv=np.exp(np.interp(zz,Z,lv)); mean=np.trapezoid(vv,zz)/180
    s=f"H={H:+d}: toward/away={v[0]/v[-1]:.1f}  k(expcos)={k1:.2f} rms={rms1:.2f} | floor+glow: F/G={F/G:.3f} k={k2:.2f} rms={rms2:.2f} | mean horizon={mean:.3g} cd/ft2"
    if H in Eh: s+=f"  E/pi={Eh[H]/math.pi:.3g}  toward/(E/pi)={v[0]/(Eh[H]/math.pi):.1f} away/(E/pi)={v[-1]/(Eh[H]/math.pi):.2f} mean/(E/pi)={mean/(Eh[H]/math.pi):.2f}"
    print(s)
# game's von Mises
from scipy.special import i0
print("game vM toward", math.exp(1.5)/i0(1.5), "away", math.exp(-1.5)/i0(1.5))
# Crumey C_inf
def Cinf(B):
    if B<0.354: return 7.633e-3*B**-0.25-7.174e-3
    return 2.720e-3
def R(B):
    if B<7.08e-2: return (6.505e-4*B**-0.25-8.461e-4)**2
    return (1.772e-4*B**-0.25+7.167e-5)**2
def dI(B):
    if B<7.08e-2: return (6.505e-4*B**0.25-8.461e-4*B**0.5)**2
    return (1.772e-4*B**0.25+7.167e-5*B**0.5)**2
print("B cd/m2, Cinf, ratio to 30cd/m2, game ratio(1+sqrt(1e-4/B)), Ricco area sr, Ricco radius arcmin, point dI lux F=1")
for B in [1e-5,3e-5,1e-4,3e-4,1e-3,3e-3,1e-2,3e-2,0.1,0.3,1,3,10,30,100,1000]:
    A=R(B)/Cinf(B); rad=math.sqrt(A/math.pi)/DR*60
    print(f"{B:g}  {Cinf(B):.4f}  {Cinf(B)/Cinf(30):.1f}  {1+math.sqrt(1e-4/B):.2f}  {A:.2e}  {rad:.1f}  {dI(B):.2e}")
# fit C_inf ratio to form 1+(Lk/L)^p over 1e-5..30
Bs=np.logspace(-5,1.5,60); y=np.array([Cinf(b)/Cinf(30) for b in Bs])
def f(B,lLk,p): return 1+(10**lLk/B)**p
p,_=curve_fit(lambda B,a,b: np.log(f(B,a,b)),Bs,np.log(y),p0=[-1,0.3])
print("fit 1+(Lk/L)^p: Lk=%.3g p=%.3f"%(10**p[0],p[1]), "max log err", np.max(np.abs(np.log(f(Bs,*p))-np.log(y))))
for B in [1e-5,1e-4,1e-3,1e-2,0.1,1]:
    print(B, f(B,*p), Cinf(B)/Cinf(30))
