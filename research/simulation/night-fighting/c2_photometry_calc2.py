import math
DR=math.pi/180
def czeh(L_mL,A,E):
    lL=math.log10(L_mL); lE=math.log10(E)
    lT=(7.0009220-0.34826531*lL+0.0086512441*lL**2+0.0099458175*lL**3-1.1146726*A+4.5215008*A**2-1.4977985*A**3
        -4.1308911*lE+0.76585211*lE**2-0.042097964*lE**3)
    return 10**lT
print("Czeh 1965 flashblindness regression T (s)")
for L in [0.0076,0.03,0.3,3]:
    for A in [0.08,0.33]:
        print(f"L={L} mL ({L*3.183:.3g} cd/m2) A={A}: ", " ".join(f"E={E:.0e}:{czeh(L,A,E):.1f}s" for E in [3.2e4,1e5,1e6,1e7,8.9e7]))
# Moon: KS phase law, relative to full (no opposition surge)
def mmag(a): return -12.73+0.026*abs(a)+4e-9*a**4
print("Moon phase law (KS91 eq 9): alpha, illuminated fraction, mag, rel to full, I* lux (sun 127000 lux at -26.74)")
for a in [0,30,45,60,90,120,135,150]:
    f=(1+math.cos(a*DR))/2
    m=mmag(a); rel=10**(-0.4*(m+12.73)); I=127000*10**(-0.4*(m+26.74))
    print(a, f"{f:.2f}", f"{m:.2f}", f"{rel:.3f}", f"{I:.3g}")
# Janiczek moon full
def atmos(HA):
    X=753.66156;U=math.sin(HA*DR);S=math.asin(X*math.cos(HA*DR)/(X+1.0));M=X*(math.cos(S)-U)+math.cos(S)
    return math.exp(-0.21*M)*U+0.0289*math.exp(-0.042*M)*(1.0+(HA+90.0)*U/57.29577951)
def jmoon(E_deg,alt):
    E=E_deg*DR
    P=0.892*math.exp(-3.343/math.tan(E/2)**0.632)+0.0344*(math.sin(E)-E*math.cos(E))
    P=0.418*P/(1-0.005*math.cos(E)-0.03*math.sin(alt*DR))
    return P*atmos(alt)
print("USNO C171 moon on horizontal (lux): elongation (180=full) x altitude")
for E in [179.9,150,120,90,60]:
    print(E, " ".join(f"{alt}:{jmoon(E,alt):.3g}" for alt in [90,60,45,30,20,10,5]))
# KS extinction for full moon horizontal, k=0.2 and 0.3
print("Full moon horizontal via KS airmass, I*=0.30 lux(no surge)")
for k in [0.2,0.3]:
    out=[]
    for alt in [90,60,45,30,20,10,5]:
        Z=(90-alt)*DR; X=1/(math.cos(Z)+0.025*math.exp(-11*math.cos(Z)))
        out.append(f"{alt}:{0.30*10**(-0.4*k*X)*math.sin(alt*DR):.3g}")
    print("k",k," ".join(out))
# night sky horizon/zenith via KS B0 X factor
for k in [0.17,0.25,0.3]:
    X=5.0; print("horizon/zenith dark sky k=",k, X*10**(-0.4*k*(X-1)))
# conversions
for V in [21.6,21.9,20.0,18.0,11.84]:
    print("V",V,"->cd/m2",10.8e4*10**(-0.4*V))
# Patat zenith fit
for z in [95,96,98,100,102,104,105]:
    a=11.84+1.518*(z-95)-0.057*(z-95)**2
    print("Patat sun",90-z,"V",round(a,2),"cd/m2 %.2g"%(10.8e4*10**(-0.4*a)))
# KTH formula
for L in [0,1e-5,1e-4,3e-4,1e-3,1e-2,1,100,1e4]:
    b=L/3.183e-6
    print("KTH L=",L,"cd/m2 i= %.2e lux"%(1e-10*math.sqrt(1+b)*10.764))
