import ephem, math
cases = [
 ("Dover 26-27 Oct 1916 drifters/Flirt", "1916/10/26 22:00", 51.0, 1.5),
 ("Dover 27 Oct 1916 Nubian/Amazon", "1916/10/27 00:45", 51.05, 1.45),
 ("Downs 23 Nov 1916", "1916/11/23 21:00", 51.4, 1.5),
 ("Dover 25 Feb 1917", "1917/02/25 23:00", 51.1, 1.6),
 ("Dover 1 Mar 1917 (Bacon's date)", "1917/03/01 23:00", 51.1, 1.6),
 ("Dover 17 Mar 1917 Paragon", "1917/03/17 22:50", 51.05, 1.6),
 ("Dover 21 Apr 1917 0045 if GMT", "1917/04/21 00:45", 51.15, 1.55),
 ("Dover 21 Apr 1917 0045 if BST", "1917/04/20 23:45", 51.15, 1.55),
 ("Dover 15 Feb 1918 0100 GMT", "1918/02/15 01:00", 50.95, 1.45),
 ("Dunkirk 21 Mar 1918 0400 GMT", "1918/03/21 04:00", 51.1, 2.4),
 ("Schouwen 23 Jan 1917 0245 GMT", "1917/01/23 02:45", 51.9, 3.0),
 ("Schouwen 23 Jan 1917 0400 GMT", "1917/01/23 04:00", 51.75, 3.4),
 ("Scarborough raid 16 Dec 1914 0515 GMT", "1914/12/16 05:15", 54.5, 2.0),
 ("Otranto 15 May 1917 0330 CEST", "1917/05/15 01:30", 40.2, 19.0),
 ("Otranto 15 May 1917 0306 AH time (CEST)", "1917/05/15 01:06", 40.6, 19.2),
 ("Durazzo 29 Dec 1915 0230 CET Monge", "1915/12/29 01:30", 42.0, 18.8),
 ("Durazzo 29 Dec 1915 1700 CET twilight", "1915/12/29 16:00", 41.6, 18.5),
 ("Trieste Wien 10 Dec 1917 0232 CET", "1917/12/10 01:32", 45.62, 13.75),
 ("Premuda 10 Jun 1918 0325 CEST", "1918/06/10 01:25", 44.3, 14.6),
 ("Gulf of Riga 17 Aug 1915 0100 local(UTC+2)", "1915/08/16 23:00", 57.7, 22.8),
 ("Gulf of Riga 17 Aug 1915 0300 local", "1915/08/17 01:00", 57.7, 22.8),
 ("Gulf of Finland 10-11 Nov 1916 2300 local", "1916/11/10 21:00", 59.5, 24.0),
 ("Dardanelles sweep 10-11 Mar 1915 0100 local", "1915/03/10 23:00", 40.1, 26.35),
 ("Dardanelles sweep 13-14 Mar 1915 0100 local", "1915/03/13 23:00", 40.1, 26.35),
 ("Coronel 1 Nov 1914 1900 local(UTC-5)", "1914/11/02 00:00", -36.9, -73.9),
 ("Coronel 1 Nov 1914 2100 local Monmouth", "1914/11/02 02:00", -36.9, -73.9),
 ("Salonika 31 Oct 1912 2335 local(UTC+2)", "1912/10/31 21:35", 40.63, 22.93),
 ("Kaliakra 21 Nov 1912 0043 local(UTC+2)", "1912/11/20 22:43", 43.3, 28.6),
 ("Dardanelles Italian TB 19 Jul 1912 0030 local", "1912/07/18 22:30", 40.1, 26.3),
]
def phase_info(d):
    m = ephem.Moon(d)
    pnm = ephem.previous_new_moon(d); nnm = ephem.next_new_moon(d)
    pfm = ephem.previous_full_moon(d); nfm = ephem.next_full_moon(d)
    age = d - pnm
    return m.phase, age, pnm, nfm
for name, t, lat, lon in cases:
    o = ephem.Observer(); o.lat = str(lat); o.lon = str(lon); o.date = t; o.pressure = 0; o.horizon='-0:34'
    m = ephem.Moon(o); s = ephem.Sun(o)
    ill, age, pnm, nfm = phase_info(ephem.Date(t))
    try: mr = o.previous_rising(ephem.Moon()); 
    except Exception: mr=None
    o.date=t
    try: ms = o.next_setting(ephem.Moon())
    except Exception: ms=None
    o.date=t
    try: mset_prev = o.previous_setting(ephem.Moon())
    except Exception: mset_prev=None
    o.date=t
    try: mr_next = o.next_rising(ephem.Moon())
    except Exception: mr_next=None
    print(f"{name}: UTC {t} | illum {ill:.0f}% age {age:.1f} d | moon alt {math.degrees(m.alt):.1f} az {math.degrees(m.az):.0f} | sun alt {math.degrees(s.alt):.1f} | moon rose {mr} sets {ms} (prev set {mset_prev}, next rise {mr_next}) | prev new {pnm} next full {nfm}")
