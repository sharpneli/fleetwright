import ephem, math
# (name, lat, lon, local offset hours from UTC used for display, local date for evening/night start, list of local times to evaluate)
cases = [
 ("Caldera 23 Apr 1891", "-27.07","-70.83", -4.72, "1891/4/22", ["22:00","1:20","3:30","4:00","4:30"]),
 ("Sta Catarina 15-16 Apr 1894", "-27.45","-48.55", -3.24, "1894/4/15", ["22:00","0:00","2:30","4:00"]),
 ("Weihaiwei 4-5 Feb 1895", "37.50","122.15", 9.0, "1895/2/4", ["22:00","0:00","3:00","4:00"]),
 ("Weihaiwei 5-6 Feb 1895", "37.50","122.15", 9.0, "1895/2/5", ["22:00","0:00","3:00","4:00"]),
 ("Santiago 2-3 Jun 1898", "19.97","-75.87", -5.06, "1898/6/2", ["22:00","1:00","3:00","3:30","4:00"]),
 ("Port Arthur 8-9 Feb 1904", "38.80","121.25", 9.0, "1904/2/8", ["22:00","23:30","0:30","2:00"]),
 ("Port Arthur 23-24 Feb 1904 blockships", "38.80","121.25", 9.0, "1904/2/23", ["22:00","0:30","3:00","4:15"]),
 ("Port Arthur 9-10 Mar 1904 (Steregushchiy)", "38.80","121.25", 9.0, "1904/3/9", ["22:00","2:00","4:00","6:00"]),
 ("Port Arthur 26-27 Mar 1904 blockships", "38.80","121.25", 9.0, "1904/3/26", ["22:00","2:20","4:00"]),
 ("Port Arthur 12-13 Apr 1904 (Strashny)", "38.80","121.25", 9.0, "1904/4/12", ["22:00","0:00","3:00","5:00"]),
 ("Port Arthur 2-3 May 1904 blockships", "38.80","121.25", 9.0, "1904/5/2", ["22:00","0:00","2:00","4:00"]),
 ("Port Arthur 23-24 Jun 1904 sortie night", "38.70","121.30", 9.0, "1904/6/23", ["21:00","22:00","0:00","2:00"]),
 ("Yellow Sea 10-11 Aug 1904", "37.80","122.80", 9.0, "1904/8/10", ["20:00","21:00","22:00","0:00","2:00","4:00"]),
 ("Dogger Bank 21-22 Oct 1904", "54.40","2.00", 0.0, "1904/10/21", ["22:00","0:00","0:55","1:30"]),
 ("Tsushima night 27-28 May 1905", "34.50","129.90", 9.0, "1905/5/27", ["19:30","20:00","21:00","22:00","23:00","0:00","2:00","3:00"]),
]
def fmt(d, off):
    t = ephem.Date(d + off/24.0).datetime()
    return t.strftime("%m-%d %H:%M")
for name, lat, lon, off, date, times in cases:
    o = ephem.Observer(); o.lat, o.lon = lat, lon; o.pressure = 0; o.horizon = '-0:34'
    # start at local noon of the date
    y,m,d = map(int, date.split('/'))
    noon_utc = ephem.Date(ephem.Date((y,m,d,12,0,0)) - off/24.0)
    o.date = noon_utc
    m_ = ephem.Moon(); s = ephem.Sun()
    out = [name]
    try:
        out.append("sunset " + fmt(o.next_setting(s), off))
        o2 = ephem.Observer(); o2.lat,o2.lon=lat,lon; o2.horizon='-12'; o2.date=noon_utc
        out.append("naut.dusk " + fmt(o2.next_setting(s, use_center=True), off))
        out.append("naut.dawn " + fmt(o2.next_rising(s, use_center=True), off))
        out.append("sunrise " + fmt(o.next_rising(s), off))
    except Exception as e: out.append(str(e))
    try: out.append("moonset " + fmt(o.next_setting(m_), off))
    except Exception as e: out.append("moonset ?")
    try: out.append("moonrise " + fmt(o.next_rising(m_), off))
    except Exception as e: out.append("moonrise ?")
    m_.compute(noon_utc + 0.5)
    out.append("illum@midnight %.0f%%" % (m_.phase))
    pn = ephem.previous_new_moon(noon_utc); nf = ephem.next_full_moon(ephem.previous_new_moon(noon_utc))
    out.append("prev new %s, full %s, next new %s" % (ephem.Date(pn).datetime().strftime('%Y-%m-%d'), ephem.Date(nf).datetime().strftime('%Y-%m-%d'), ephem.Date(ephem.next_new_moon(noon_utc)).datetime().strftime('%Y-%m-%d')))
    print(" | ".join(out))
    for t in times:
        hh, mm = map(int, t.split(':'))
        dd = d if hh >= 12 else d+1
        utc = ephem.Date(ephem.Date((y,m,d,hh,mm,0)) + (0 if hh>=12 else 1) - off/24.0)
        o.date = utc; m_.compute(o); s.compute(o)
        print("   %s local: moon alt %5.1f az %5.1f  sun alt %6.1f" % (t, math.degrees(m_.alt), math.degrees(m_.az), math.degrees(s.alt)))
