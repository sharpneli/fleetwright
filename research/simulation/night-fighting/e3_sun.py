import ephem, math
def sun(name, date, lat, lon):
    o=ephem.Observer(); o.lat=str(lat); o.lon=str(lon); o.date=date; o.pressure=0; o.horizon='-0:50'
    r=o.next_rising(ephem.Sun()); o.date=date; s=o.next_setting(ephem.Sun())
    o.horizon='-12'; o.date=date; nr=o.next_rising(ephem.Sun(), use_center=True); o.date=date; ns=o.next_setting(ephem.Sun(), use_center=True)
    print(f"{name}: sunrise {r} UTC, sunset {s} UTC, nautical dawn {nr}, nautical dusk {ns}")
sun("Otranto 15 May 1917", "1917/05/14 12:00", 40.2, 19.0)
sun("Premuda 10 Jun 1918", "1918/06/09 12:00", 44.3, 14.6)
sun("Durazzo 29 Dec 1915", "1915/12/29 00:00", 41.5, 18.5)
sun("Coronel 1 Nov 1914", "1914/11/01 12:00", -36.9, -73.9)
sun("Dover 20 Apr 1917", "1917/04/20 12:00", 51.1, 1.4)
sun("Scarborough 16 Dec 1914", "1914/12/15 12:00", 54.5, 2.0)
sun("Gulf of Riga 16 Aug 1915", "1915/08/16 06:00", 57.7, 22.8)
