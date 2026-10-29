"""Sun and moon at the night cases (PyEphem, no refraction): elevation, bearing, moon illuminated share."""
import ephem, math
CASES = [  # name, lat, lon, UT times
 ('Weihaiwei 4-5 Feb 1895 (attack ~03:00 local = 19:00 UT on the 4th)', '37.5', '122.15', ['1895/2/4 18:00', '1895/2/4 19:00']),
 ('Weihaiwei 5-6 Feb 1895', '37.5', '122.15', ['1895/2/5 18:00', '1895/2/5 19:00']),
 ('Port Arthur 8-9 Feb 1904 (attack ~23:30 local = 15:25 UT)', '38.8', '121.25', ['1904/2/8 15:30', '1904/2/8 17:00']),
 ('Yellow Sea night 10-11 Aug 1904', '37.0', '123.0', ['1904/8/10 11:00', '1904/8/10 13:00', '1904/8/10 16:00']),
 ('Dogger Bank incident 21-22 Oct 1904 (~00:55 local)', '54.5', '2.0', ['1904/10/21 21:00', '1904/10/22 00:00']),
 ('Tsushima night 27-28 May 1905 (local = UT+9)', '34.5', '130.0', ['1905/5/27 11:00', '1905/5/27 12:00', '1905/5/27 13:00', '1905/5/27 15:00', '1905/5/27 17:00']),
 ('Coronel 1 Nov 1914 (local ~UT-4.9)', '-36.9', '-73.8', ['1914/11/1 23:30', '1914/11/2 00:30', '1914/11/2 01:30']),
 ('Gulf of Riga 16-17 Aug 1915', '57.5', '23.0', ['1915/8/16 22:00', '1915/8/17 01:00']),
 ('Jutland night 31 May-1 Jun 1916 (GMT)', '56.6', '5.8', ['1916/5/31 20:00', '1916/5/31 21:00', '1916/5/31 22:00', '1916/5/31 23:00', '1916/6/1 00:00', '1916/6/1 01:00', '1916/6/1 02:00']),
 ('Dover Strait 26-27 Oct 1916', '51.1', '1.6', ['1916/10/26 22:00', '1916/10/27 00:00']),
 ('Dover Strait 20-21 Apr 1917', '51.1', '1.6', ['1917/4/20 23:00', '1917/4/21 00:45']),
 ('Otranto 14-15 May 1917 (local = UT+1)', '40.2', '18.9', ['1917/5/15 01:00', '1917/5/15 03:30']),
 ('Dover Strait 14-15 Feb 1918', '51.1', '1.6', ['1918/2/15 00:00', '1918/2/15 01:30']),
]
for name, lat, lon, times in CASES:
    o = ephem.Observer(); o.lat, o.lon, o.pressure = lat, lon, 0
    print(name)
    for t in times:
        o.date = t; s = ephem.Sun(o); m = ephem.Moon(o)
        print(f'  {t} UT  sun {math.degrees(s.alt):6.1f} deg az {math.degrees(s.az):4.0f} | moon {math.degrees(m.alt):6.1f} deg az {math.degrees(m.az):4.0f}, {m.phase:3.0f}% lit')
