// Scenario for the map mockup: 31 May 1916, 16:42 GMT. Illustrative, not a historical record.
'use strict';
const START_MS = Date.UTC(1916, 4, 31, 16, 42, 0);
const BATTLE_O = [56.60, 5.90];   // origin of the Horns Reef action, rel positions are km N / km E of this

const TYPES = {
  BB: { n: 'Battleship', L: 190, k: 'cap', kt: 27 },
  BC: { n: 'Battlecruiser', L: 205, k: 'cap', kt: 28 },
  PB: { n: 'Pre-dreadnought', L: 132, k: 'cap', kt: 15 },
  CA: { n: 'Armoured cruiser', L: 150, k: 'cru', kt: 13 },
  CL: { n: 'Light cruiser', L: 135, k: 'cru', kt: 4.5 },
  DL: { n: 'Flotilla leader', L: 100, k: 'lt', kt: 1.6 },
  DD: { n: 'Destroyer', L: 84, k: 'lt', kt: 1.0 },
  SS: { n: 'Submarine', L: 65, k: 'lt', kt: 0.7 },
  SL: { n: 'Sloop', L: 80, k: 'lt', kt: 1.2 },
  AV: { n: 'Seaplane carrier', L: 96, k: 'aux', kt: 2.5 },
  MON: { n: 'Monitor', L: 100, k: 'aux', kt: 6 },
  AUX: { n: 'Auxiliary', L: 110, k: 'aux', kt: 6 },
  MV: { n: 'Merchant', L: 125, k: 'mer', kt: 7 },
};

const SIDES = {
  RN: { name: 'Royal Navy', aff: 'F', own: true },
  FR: { name: 'Marine nationale', aff: 'F', ally: true },
  IT: { name: 'Regia Marina', aff: 'F', ally: true },
  RU: { name: 'Imperial Russian Navy', aff: 'F', ally: true },
  JP: { name: 'Imperial Japanese Navy', aff: 'F', ally: true },
  DE: { name: 'Kaiserliche Marine', aff: 'H' },
  AH: { name: 'k.u.k. Kriegsmarine', aff: 'H' },
  OT: { name: 'Ottoman Navy', aff: 'H' },
  US: { name: 'US Navy', aff: 'N' },
  MER: { name: 'Merchant', aff: 'F', ally: true },
};

const CLASSES = {
  'Lion': 'Lion', 'Princess Royal': 'Lion', 'Queen Mary': 'Queen Mary', 'Tiger': 'Tiger',
  'New Zealand': 'Indefatigable', 'Indefatigable': 'Indefatigable', 'Australia': 'Indefatigable',
  'Barham': 'Queen Elizabeth', 'Valiant': 'Queen Elizabeth', 'Warspite': 'Queen Elizabeth', 'Malaya': 'Queen Elizabeth',
  'Invincible': 'Invincible', 'Inflexible': 'Invincible', 'Indomitable': 'Invincible',
  'Iron Duke': 'Iron Duke', 'Benbow': 'Iron Duke', 'Marlborough': 'Iron Duke', 'Emperor of India': 'Iron Duke',
  'King George V': 'King George V', 'Ajax': 'King George V', 'Centurion': 'King George V',
  'Orion': 'Orion', 'Monarch': 'Orion', 'Conqueror': 'Orion', 'Thunderer': 'Orion',
  'Royal Oak': 'Revenge', 'Revenge': 'Revenge', 'Royal Sovereign': 'Revenge', 'Ramillies': 'Revenge', 'Resolution': 'Revenge',
  'Superb': 'Bellerophon', 'Bellerophon': 'Bellerophon', 'Temeraire': 'Bellerophon',
  'Canada': 'Canada', 'Erin': 'Erin', 'Agincourt': 'Agincourt',
  'Vanguard': 'St Vincent', 'Collingwood': 'St Vincent', 'St Vincent': 'St Vincent',
  'Hercules': 'Colossus', 'Colossus': 'Colossus', 'Neptune': 'Neptune',
  'Lützow': 'Derfflinger', 'Derfflinger': 'Derfflinger', 'Seydlitz': 'Seydlitz', 'Moltke': 'Moltke', 'Von der Tann': 'Von der Tann',
  'Hood': 'Admiral (1916 design)', 'Repulse': 'Renown', 'Renown': 'Renown', 'Courageous': 'Courageous', 'Glorious': 'Courageous',
  'Lord Nelson': 'Lord Nelson', 'Agamemnon': 'Lord Nelson',
};

const DDN = ('Onslow Moresby Nicator Nestor Nomad Narborough Obdurate Petard Pelican Nerissa Turbulent Termagant Morris Moorsom ' +
  'Lydiard Landrail Liberty Laurel Lapwing Lochinvar Lassoo Mons Marne Manners Mystic Maenad Marvel Mindful Mary_Rose Menace ' +
  'Opal Ophelia Owl Onslaught Obedient Oak Martial Magic Mandate Medway Michael Milbrook Minion Mischief Moon Morning_Star ' +
  'Munster Napier Noble Nonsuch Norman Northesk Nugent Octavia Offa Orcadia Oriole Orpheus Osiris Pigeon Plover Portia ' +
  'Penn Peregrine Phoebe Pylades Achates Ambuscade Ardent Fortune Porpoise Unity Garland Contest Christopher Ophelia Shark ' +
  'Acasta Spitfire Sparrowhawk Broke Hardy Midge Owl Wizard Tipperary Turbulent Sylph Saxon Skate Sorceress').split(' ').map(s => s.replace('_', ' '));
let _dd = 0;
const dd = (n) => Array.from({ length: n }, () => 'DD ' + DDN[(_dd++) % DDN.length]).join(';');
let _gd = 1;
const gdd = (n, pfx) => Array.from({ length: n }, () => 'DD ' + pfx + (20 + (_gd++) * 3)).join(';');

// form: line | cols | scout | screen | port | dock | slip | convoy | single
// rel: km north / east of BATTLE_O; at: [lat, lon]
const UNITS = [
  // ---- Battlecruiser Fleet: in action
  { id: 'bcs1', side: 'RN', fleet: 'Battlecruiser Fleet', name: '1st Battlecruiser Sqn', short: '1st BCS', cmdr: 'V-Adm Beatty (Lion)', rel: [2, -6], hdg: 150, kn: 25, form: 'line',
    ships: 'BC Lion;BC Princess Royal;BC Queen Mary;BC Tiger', enc: 'E1', task: 'Engage I SG; draw it north onto the Grand Fleet', ctl: 'DIRECT', wps: [] },
  { id: 'bcs2', side: 'RN', fleet: 'Battlecruiser Fleet', name: '2nd Battlecruiser Sqn', short: '2nd BCS', cmdr: 'R-Adm Pakenham (New Zealand)', rel: [4.2, -7.3], hdg: 150, kn: 25, form: 'line',
    ships: 'BC New Zealand', enc: 'E1', task: 'Follow 1st BCS (in succession)', ctl: 'TASK' },
  { id: 'bs5', side: 'RN', fleet: 'Battlecruiser Fleet', name: '5th Battle Sqn', short: '5th BS', cmdr: 'R-Adm Evan-Thomas (Barham)', rel: [10.5, -11.5], hdg: 150, kn: 24, form: 'line',
    ships: 'BB Barham;BB Valiant;BB Warspite;BB Malaya', enc: 'E1', task: 'Support BCF; engage rear of I SG', ctl: 'TASK', sent: { hdg: 135, at: '16:38', transit: 100 } },
  { id: 'lcs1', side: 'RN', fleet: 'Battlecruiser Fleet', name: '1st Light Cruiser Sqn', short: '1st LCS', cmdr: 'Cdre Alexander-Sinclair', rel: [13, -2], hdg: 150, kn: 25, form: 'scout',
    ships: 'CL Galatea;CL Phaeton;CL Inconstant;CL Cordelia', enc: 'E1', task: 'Screen BCF to NE', ctl: 'DOCTRINE' },
  { id: 'lcs2', side: 'RN', fleet: 'Battlecruiser Fleet', name: '2nd Light Cruiser Sqn', short: '2nd LCS', cmdr: 'Cdre Goodenough (Southampton)', rel: [-21, 1.5], hdg: 150, kn: 25, form: 'scout',
    ships: 'CL Southampton;CL Birmingham;CL Nottingham;CL Dublin', enc: 'E1', task: 'Scout ahead of BCF to SSE; report heavy ships', ctl: 'TASK' },
  { id: 'lcs3', side: 'RN', fleet: 'Battlecruiser Fleet', name: '3rd Light Cruiser Sqn', short: '3rd LCS', cmdr: 'R-Adm Napier (Falmouth)', rel: [6, -14], hdg: 150, kn: 25, form: 'scout',
    ships: 'CL Falmouth;CL Yarmouth;CL Birkenhead;CL Gloucester', enc: 'E1', task: 'Screen disengaged side', ctl: 'DOCTRINE' },
  { id: 'fl13', side: 'RN', fleet: 'Battlecruiser Fleet', name: '13th Flotilla', short: '13th Fl', cmdr: 'Capt Farie (Champion)', rel: [-1.5, -0.5], hdg: 95, kn: 30, form: 'line',
    ships: 'CL Champion;' + dd(8), enc: 'E1', task: 'Torpedo attack on I SG', ctl: 'TASK', wps: [{ rel: [-3.5, 7.5] }] },
  { id: 'fl9', side: 'RN', fleet: 'Battlecruiser Fleet', name: '9th/10th Flotilla', short: '9th Fl', cmdr: 'Cdr Goldsmith', rel: [3, -10], hdg: 150, kn: 25, form: 'screen',
    ships: dd(8), enc: 'E1', task: 'Screen 1st BCS against torpedo attack', ctl: 'DOCTRINE' },
  { id: 'eng', side: 'RN', fleet: 'Battlecruiser Fleet', name: 'Engadine', short: 'Engadine', cmdr: 'Lt-Cdr Robinson', rel: [19, -9], hdg: 150, kn: 18, form: 'single',
    ships: 'AV Engadine', enc: 'E1', task: 'Seaplane recce to SE (1 aloft)', ctl: 'TASK' },
  // ---- Grand Fleet: closing from the NNW
  { id: 'bs4', side: 'RN', fleet: 'Grand Fleet', name: '4th Battle Sqn', short: '4th BS', cmdr: 'Adm Jellicoe (Iron Duke)', rel: [88, -44], hdg: 140, kn: 20, form: 'cols',
    ships: 'BB Iron Duke;BB Royal Oak;BB Superb;BB Canada;BB Benbow;BB Bellerophon;BB Temeraire;BB Vanguard', task: 'Close BCF at best speed; deploy on contact', ctl: 'DIRECT', wps: [{ rel: [20, -2] }] },
  { id: 'bs2', side: 'RN', fleet: 'Grand Fleet', name: '2nd Battle Sqn', short: '2nd BS', cmdr: 'V-Adm Jerram (King George V)', rel: [89, -48], hdg: 140, kn: 20, form: 'cols',
    ships: 'BB King George V;BB Ajax;BB Centurion;BB Erin;BB Orion;BB Monarch;BB Conqueror;BB Thunderer', task: 'Cruising disposition, port wing', ctl: 'TASK', wps: [{ rel: [21, -6] }] },
  { id: 'bs1', side: 'RN', fleet: 'Grand Fleet', name: '1st Battle Sqn', short: '1st BS', cmdr: 'V-Adm Burney (Marlborough)', rel: [87, -40], hdg: 140, kn: 20, form: 'cols',
    ships: 'BB Marlborough;BB Revenge;BB Hercules;BB Agincourt;BB Colossus;BB Collingwood;BB Neptune;BB St Vincent', task: 'Cruising disposition, starboard wing', ctl: 'TASK', wps: [{ rel: [19, 2] }] },
  { id: 'bcs3', side: 'RN', fleet: 'Grand Fleet', name: '3rd Battlecruiser Sqn', short: '3rd BCS', cmdr: 'R-Adm Hood (Invincible)', rel: [42, 2], hdg: 160, kn: 25, form: 'line',
    ships: 'BC Invincible;BC Inflexible;BC Indomitable', task: 'Reinforce BCF', ctl: 'TASK', wps: [{ rel: [5, 6] }] },
  { id: 'cs1', side: 'RN', fleet: 'Grand Fleet', name: '1st/2nd Cruiser Sqn', short: '1st CS', cmdr: 'R-Adm Arbuthnot (Defence)', rel: [72, -30], hdg: 140, kn: 20, form: 'scout',
    ships: 'CA Defence;CA Warrior;CA Duke of Edinburgh;CA Black Prince;CA Minotaur;CA Hampshire;CA Cochrane;CA Shannon', task: 'Advanced screen 16 nm ahead', ctl: 'DOCTRINE' },
  { id: 'lcs4', side: 'RN', fleet: 'Grand Fleet', name: '4th Light Cruiser Sqn', short: '4th LCS', cmdr: 'Cdre Le Mesurier', rel: [80, -40], hdg: 140, kn: 20, form: 'scout',
    ships: 'CL Calliope;CL Constance;CL Caroline;CL Royalist;CL Comus', task: 'Close screen', ctl: 'DOCTRINE' },
  { id: 'fl4', side: 'RN', fleet: 'Grand Fleet', name: '4th, 11th, 12th Flotillas', short: 'GF flotillas', cmdr: 'Capt Wintour (Tipperary)', rel: [84, -44], hdg: 140, kn: 20, form: 'screen',
    ships: 'DL Tipperary;' + dd(20), task: 'A/S screen', ctl: 'DOCTRINE' },
  // ---- Home ports, docks, yards
  { id: 'hf', side: 'RN', fleet: 'Harwich Force', name: 'Harwich Force', short: 'Harwich F', cmdr: 'Cdre Tyrwhitt (Arethusa)', at: [51.948, 1.30], form: 'port', status: 'PORT', port: 'Harwich',
    ships: 'CL Arethusa;CL Penelope;CL Undaunted;CL Cleopatra;CL Conquest;CL Carysfort;CL Aurora;' + dd(10), task: 'Held in harbour by Admiralty (anticipating raid on coast)', ctl: 'TASK' },
  { id: 'aus', side: 'RN', fleet: 'Home ports', name: 'Australia (dock)', short: 'Australia', cmdr: 'Capt Radcliffe', at: [56.0215, -3.4440], hdg: 90, form: 'dock', status: 'DOCK', port: 'Rosyth',
    ships: 'BC Australia', task: 'Collision repair, No.1 Dock: 11 days', ctl: '—' },
  { id: 'ros', side: 'RN', fleet: 'Home ports', name: 'Rosyth refit', short: 'Rosyth', cmdr: '—', at: [56.012, -3.425], hdg: 250, form: 'port', status: 'PORT', port: 'Rosyth',
    ships: 'CL Blanche;' + dd(3), task: 'Boiler cleaning, 2 days', ctl: '—' },
  { id: 'scapa', side: 'RN', fleet: 'Home ports', name: 'Scapa Flow', short: 'Scapa', cmdr: 'R-Adm Miller', at: [58.86, -3.10], hdg: 270, form: 'port', status: 'PORT', port: 'Scapa Flow',
    ships: 'BB Royal Sovereign;AUX Cyclops;AUX Assistance;AUX Collier 7;AUX Collier 12', task: 'Royal Sovereign working up (gunnery 41%)', ctl: '—' },
  { id: 'chf', side: 'RN', fleet: 'Channel Fleet', name: 'Channel Fleet (old BB)', short: 'Channel F', cmdr: 'V-Adm Bethell', at: [50.565, -2.43], hdg: 90, form: 'port', status: 'PORT', port: 'Portland',
    ships: 'PB Prince of Wales;PB Venerable;PB London;PB Exmouth;PB Queen;PB Implacable', task: 'Reserve for invasion defence', ctl: 'TASK' },
  { id: 'dov', side: 'RN', fleet: 'Dover Patrol', name: 'Dover Patrol', short: 'Dover', cmdr: 'V-Adm Bacon', at: [51.25, 1.75], hdg: 60, kn: 6, form: 'line',
    ships: 'MON Marshal Ney;MON Prince Rupert;' + dd(6), task: 'Patrol Dover barrage', ctl: 'TASK', wps: [{ at: [51.38, 2.25] }, { at: [51.1, 1.6] }] },
  { id: 'b_hood', side: 'RN', fleet: 'Building', name: 'Hood (slip 4)', short: 'Hood', cmdr: 'John Brown, Clydebank', at: [55.9010, -4.4030], hdg: 200, form: 'slip', status: 'BUILD', port: 'Clydebank',
    ships: 'BC Hood', build: { p: 0.01, stage: 'Laid down today', done: 'Jan 1920' }, task: 'Laid down 31 May 1916', ctl: '—' },
  { id: 'b_rep', side: 'RN', fleet: 'Building', name: 'Repulse (fitting out)', short: 'Repulse', cmdr: 'John Brown, Clydebank', at: [55.9010, -4.3975], hdg: 200, form: 'slip', status: 'FIT', port: 'Clydebank',
    ships: 'BC Repulse', build: { p: 0.91, stage: 'Fitting out', done: 'Aug 1916' }, task: 'Fitting out, basin', ctl: '—' },
  { id: 'b_ren', side: 'RN', fleet: 'Building', name: 'Renown (fitting out)', short: 'Renown', cmdr: 'Fairfield, Govan', at: [55.866, -4.315], hdg: 280, form: 'slip', status: 'FIT', port: 'Govan',
    ships: 'BC Renown', build: { p: 0.88, stage: 'Fitting out', done: 'Sep 1916' }, task: 'Fitting out', ctl: '—' },
  { id: 'b_ram', side: 'RN', fleet: 'Building', name: 'Ramillies (fitting out)', short: 'Ramillies', cmdr: 'Beardmore, Dalmuir', at: [55.912, -4.43], hdg: 290, form: 'slip', status: 'FIT', port: 'Dalmuir',
    ships: 'BB Ramillies', build: { p: 0.79, stage: 'Fitting out', done: 'Sep 1917' }, task: 'Fitting out; damaged on launch', ctl: '—' },
  { id: 'b_cou', side: 'RN', fleet: 'Building', name: 'Courageous (slip)', short: 'Courageous', cmdr: 'Armstrong, Elswick', at: [54.968, -1.655], hdg: 200, form: 'slip', status: 'BUILD', port: 'Elswick',
    ships: 'BC Courageous', build: { p: 0.58, stage: 'On the slip, launch Feb 1916+', done: 'Jan 1917' }, task: 'Building', ctl: '—' },
  { id: 'b_glo', side: 'RN', fleet: 'Building', name: 'Glorious (slip)', short: 'Glorious', cmdr: 'Harland & Wolff, Belfast', at: [54.61, -5.905], hdg: 320, form: 'slip', status: 'BUILD', port: 'Belfast',
    ships: 'BC Glorious', build: { p: 0.62, stage: 'Launched Apr 1916', done: 'Jan 1917' }, task: 'Building', ctl: '—' },
  { id: 'b_res', side: 'RN', fleet: 'Building', name: 'Resolution (fitting out)', short: 'Resolution', cmdr: 'Palmers, Jarrow', at: [54.983, -1.49], hdg: 90, form: 'slip', status: 'FIT', port: 'Jarrow',
    ships: 'BB Resolution', build: { p: 0.9, stage: 'Fitting out', done: 'Dec 1916' }, task: 'Fitting out', ctl: '—' },
  // ---- Western Approaches convoy (E3)
  { id: 'oq12', side: 'RN', fleet: 'Western Approaches', name: 'Convoy OQ-12', short: 'OQ-12', cmdr: 'Cdre (RNR) Hext · 2 sloops', at: [49.55, -12.4], hdg: 62, kn: 8, form: 'convoy',
    ships: 'SL Lavender;SL Poppy;MV Clan Ross;MV City of Exeter;MV Anglo-Saxon;MV Kildonan Castle;MV Huntsend;MV War Pansy;MV Harpalyce;MV Ruel', enc: 'E3', task: 'Escort OQ-12 to Plymouth', ctl: 'TASK', wps: [{ at: [50.2, -4.2] }] },
  // ---- Abroad
  { id: 'emed', side: 'RN', fleet: 'Eastern Mediterranean', name: 'Eastern Med Sqn', short: 'E. Med', cmdr: 'R-Adm Fremantle', at: [39.865, 25.27], hdg: 200, form: 'port', status: 'PORT', port: 'Mudros',
    ships: 'PB Lord Nelson;PB Agamemnon;PB Cornwallis;CL Talbot;' + dd(4), task: 'Watch the Dardanelles (Goeben)', ctl: 'TASK' },
  { id: 'adr', side: 'RN', fleet: 'Adriatic', name: '8th Light Cruiser Sqn', short: '8th LCS', cmdr: 'Cdre Kelly', at: [41.2, 18.25], hdg: 320, kn: 14, form: 'scout',
    ships: 'CL Weymouth;CL Dartmouth;CL Bristol', task: 'Otranto barrage support', ctl: 'TASK', wps: [{ at: [40.65, 18.6] }] },
  { id: 'malta', side: 'RN', fleet: 'Mediterranean', name: 'Malta refit', short: 'Malta', cmdr: '—', at: [35.888, 14.52], hdg: 0, form: 'port', status: 'DOCK', port: 'Malta',
    ships: 'PB Russell;' + dd(2), task: 'Docked: Russell, refit 31 d', ctl: '—' },
  { id: 'gib', side: 'RN', fleet: 'Mediterranean', name: 'Gibraltar Patrol', short: 'Gibraltar', cmdr: '—', at: [36.13, -5.35], form: 'port', status: 'PORT', port: 'Gibraltar',
    ships: 'SL Rosemary;SL Mignonette;' + dd(2), task: 'Strait patrol', ctl: 'TASK' },
  { id: 'satl', side: 'RN', fleet: 'South Atlantic', name: 'S. Atlantic hunting group', short: 'Kent grp', cmdr: 'Capt Allen (Kent)', at: [-18.05, -37.45], hdg: 205, kn: 22, form: 'scout',
    ships: 'CA Kent;CL Glasgow;AUX Orama', enc: 'E2', task: 'Run down raider Möwe', ctl: 'DIRECT' },
  { id: 'falk', side: 'RN', fleet: 'South Atlantic', name: 'Carnarvon', short: 'Carnarvon', cmdr: '—', at: [-51.69, -57.83], form: 'port', status: 'PORT', port: 'Port Stanley',
    ships: 'CA Carnarvon', task: 'Guardship', ctl: '—' },
  { id: 'nawi', side: 'RN', fleet: 'N. America & WI', name: 'N. America & WI Sqn', short: 'NA&WI', cmdr: 'V-Adm Browning', at: [40.6, -66.0], hdg: 50, kn: 12, form: 'scout',
    ships: 'CA Leviathan;CA Berwick;CA Lancaster;AUX Caronia', task: 'Patrol off New York (watch interned liners)', ctl: 'TASK', wps: [{ at: [41.8, -62] }] },
  { id: 'hal', side: 'RN', fleet: 'N. America & WI', name: 'Halifax', short: 'Halifax', cmdr: '—', at: [44.64, -63.56], form: 'port', status: 'PORT', port: 'Halifax',
    ships: 'CL Niobe;AUX Changuinola', task: 'Base', ctl: '—' },
  { id: 'cape', side: 'RN', fleet: 'Cape', name: 'Cape Sqn', short: 'Cape', cmdr: 'R-Adm King-Hall', at: [-34.19, 18.44], form: 'port', status: 'PORT', port: "Simon's Town",
    ships: 'CL Hyacinth;CL Challenger', task: 'Base', ctl: '—' },
  { id: 'ei', side: 'RN', fleet: 'East Indies', name: 'East Indies Sqn', short: 'E. Indies', cmdr: 'R-Adm Wemyss', at: [12.5, 52.0], hdg: 250, kn: 11, form: 'scout',
    ships: 'PB Swiftsure;CL Philomel;AUX Empress of Asia', task: 'Escort troop convoy Bombay → Suez', ctl: 'TASK', wps: [{ at: [12.6, 43.4] }] },
  { id: 'china', side: 'RN', fleet: 'China', name: 'China Station', short: 'China', cmdr: 'V-Adm Grant', at: [22.29, 114.17], form: 'port', status: 'PORT', port: 'Hong Kong',
    ships: 'CA Cornwall;CL Newcastle;' + dd(3), task: 'Base', ctl: '—' },
  { id: 'aust', side: 'RN', fleet: 'Australia', name: 'Australian Sqn', short: 'RAN', cmdr: 'R-Adm Patey', at: [-33.84, 151.24], form: 'port', status: 'PORT', port: 'Sydney',
    ships: 'CL Melbourne;CL Brisbane;' + dd(3), task: 'Base', ctl: '—' },
  // ---- Allies (not commandable)
  { id: 'fr', side: 'FR', fleet: 'Armée navale', name: 'Armée navale', short: 'French BF', cmdr: 'V-Adm Dartige du Fournet', at: [39.62, 19.92], form: 'port', status: 'PORT', port: 'Corfu',
    ships: 'BB Bretagne;BB Provence;BB Lorraine;BB Courbet;BB Jean Bart;BB France;BB Paris;PB Danton;PB Voltaire;' + dd(6), ally: true },
  { id: 'it', side: 'IT', fleet: 'Regia Marina', name: 'Regia Marina', short: 'Italian BF', at: [40.465, 17.22], form: 'port', status: 'PORT', port: 'Taranto',
    ships: 'BB Conte di Cavour;BB Giulio Cesare;BB Andrea Doria;BB Caio Duilio;BB Dante Alighieri;' + dd(8), ally: true },
  { id: 'ru', side: 'RU', fleet: 'Baltic Fleet', name: 'Baltic Fleet', short: 'Russian BF', at: [60.155, 24.96], form: 'port', status: 'PORT', port: 'Helsingfors',
    ships: 'BB Gangut;BB Poltava;BB Petropavlovsk;BB Sevastopol;CA Rurik;' + dd(8), ally: true },
  { id: 'rubs', side: 'RU', fleet: 'Black Sea Fleet', name: 'Black Sea Fleet', short: 'Black Sea', at: [44.62, 33.52], form: 'port', status: 'PORT', port: 'Sevastopol',
    ships: 'BB Imperatritsa Mariya;BB Imperatritsa Ekaterina;PB Evstafi;PB Ioann Zlatoust;' + dd(6), ally: true },
  { id: 'jp', side: 'JP', fleet: 'IJN', name: 'IJN 1st Fleet', short: 'IJN', at: [34.23, 132.55], form: 'port', status: 'PORT', port: 'Kure',
    ships: 'BC Kongo;BC Hiei;BC Kirishima;BC Haruna;BB Fuso;BB Settsu;BB Kawachi;' + dd(6), ally: true },
  // ---- Neutral
  { id: 'us', side: 'US', fleet: 'Atlantic Fleet', name: 'US Atlantic Fleet', short: 'USN', at: [36.97, -76.30], form: 'port', status: 'PORT', port: 'Hampton Roads',
    ships: 'BB New York;BB Texas;BB Wyoming;BB Arkansas;BB Florida;BB Utah;BB Delaware;BB North Dakota;' + dd(6), neutral: true },
  // ---- Enemy (the truth; the player sees them only through intel)
  { id: 'sg1', side: 'DE', fleet: 'Hochseeflotte', name: 'I Scouting Group', short: 'I SG', cmdr: 'V-Adm Hipper (Lützow)', rel: [0.6, 8.6], hdg: 150, kn: 25, form: 'line',
    ships: 'BC Lützow;BC Derfflinger;BC Seydlitz;BC Moltke;BC Von der Tann', enc: 'E1', intel: { cert: 'id', src: 'visual', age: 0, by: 'Lion, Tiger, Barham' } },
  { id: 'sg2', side: 'DE', fleet: 'Hochseeflotte', name: 'II Scouting Group', short: 'II SG', rel: [6.5, 13.5], hdg: 150, kn: 25, form: 'scout',
    ships: 'CL Frankfurt;CL Wiesbaden;CL Pillau;CL Elbing', enc: 'E1', intel: { cert: 'sus', src: 'visual', age: 3, by: 'Galatea', est: '3–4 CL?' } },
  { id: 'dfl', side: 'DE', fleet: 'Hochseeflotte', name: 'IX Torpedo-boat Flotilla', short: 'IX TBF', rel: [-3.0, 6.5], hdg: 285, kn: 30, form: 'screen',
    ships: gdd(8, 'V'), enc: 'E1', intel: { cert: 'sus', src: 'visual', age: 1, by: 'Champion', est: '6+ TB?' } },
  { id: 'hsf3', side: 'DE', fleet: 'Hochseeflotte', name: 'III Squadron', short: 'III Sq', rel: [-47, 11], hdg: 335, kn: 17, form: 'line',
    ships: 'BB König;BB Grosser Kurfürst;BB Markgraf;BB Kronprinz;BB Kaiser;BB Kaiserin;BB Prinzregent Luitpold;BB Friedrich der Grosse', intel: { cert: 'none' } },
  { id: 'hsf1', side: 'DE', fleet: 'Hochseeflotte', name: 'I Squadron', short: 'I Sq', rel: [-52, 13.5], hdg: 335, kn: 17, form: 'line',
    ships: 'BB Ostfriesland;BB Thüringen;BB Helgoland;BB Oldenburg;BB Posen;BB Rheinland;BB Nassau;BB Westfalen', intel: { cert: 'none' } },
  { id: 'moewe', side: 'DE', fleet: 'Raiders', name: 'Möwe (raider)', short: 'Möwe', rel: null, at: [-18.16, -37.53], hdg: 215, kn: 14, form: 'single',
    ships: 'AUX Möwe', enc: 'E2', intel: { cert: 'id', src: 'visual', age: 0, by: 'Kent' } },
  { id: 'ub', side: 'DE', fleet: 'U-boats', name: 'U-boat', short: 'U-boat', at: [49.60, -12.25], hdg: 250, kn: 3, form: 'single',
    ships: 'SS U 24', enc: 'E3', intel: { cert: 'sus', src: 'visual', age: 2, by: 'Lavender (periscope)', est: 'SS?' } },
];

// What the player knows of enemy forces not on the plot as ships: shapes of doubt.
const CONTACTS = [
  { id: 'c_smoke', kind: 'ellipse', side: 'DE', title: 'Heavy ships? (smoke)', from: 'lcs2', brg: 148, rng: [16, 30], est: { cap: [6, 16], cru: [0, 4], lt: [0, 12] },
    reports: ['16:38 Southampton (visual): "Smoke and masts, bearing 148, ~11 nm, many ships"', '16:41 Birmingham (visual): "Large ships, bearing 150"'], age: 4, area: 'North Sea', truthNear: 'hsf3', sameAs: 'c_df' },
  { id: 'c_df', kind: 'df', side: 'DE', title: 'Callsign DK (HSF flagship?)', stations: [{ name: 'DF Hunstanton', at: [52.95, 0.50] }, { name: 'DF Flamborough', at: [54.12, -0.08] }], truth: 'hsf3', err: [1.6, -1.2], width: 2.0,
    est: { cap: [16, 22], cru: [0, 6], lt: [0, 30] }, reports: ['16:20 Room 40: DK transmitting, fixed 55°55′N 6°40′E ±15 nm', '16:20 Hunstanton brg 064.6°', '16:20 Flamborough brg 076.1°'], age: 22, area: 'North Sea' },
  { id: 'c_wil', kind: 'port', side: 'DE', title: 'Jade / Wilhelmshaven', at: [53.52, 8.14], est: { cap: [2, 6], cru: [1, 3], lt: [4, 20] }, reports: ['29 May, agent: "Fleet raising steam in the Jade"', '31 May 14:10 Room 40: HSF at sea since 02:00?'], age: 1500, area: 'North Sea' },
  { id: 'c_kiel', kind: 'port', side: 'DE', title: 'Kiel', at: [54.33, 10.15], est: { cap: [1, 3], cru: [0, 2], lt: [2, 10] }, reports: ['17 May, neutral merchant: "Baden fitting out; Bayern in the basin"'], age: 20000, area: 'Baltic' },
  { id: 'c_pola', kind: 'port', side: 'AH', title: 'Pola', at: [44.87, 13.84], est: { cap: [6, 7], cru: [2, 3], lt: [8, 14] }, reports: ['29 May 07:30, seaplane from Brindisi: 4 dreadnoughts, 3 Radetzky at anchor'], age: 3400, area: 'Mediterranean' },
  { id: 'c_cat', kind: 'port', side: 'AH', title: 'Cattaro', at: [42.43, 18.69], est: { cap: [0, 1], cru: [2, 3], lt: [4, 8] }, reports: ['26 May, agent: Novara, Helgoland, destroyers'], age: 7500, area: 'Mediterranean' },
  { id: 'c_con', kind: 'port', side: 'OT', title: 'Constantinople', at: [41.01, 28.98], est: { cap: [1, 1], cru: [1, 1], lt: [2, 6] }, reports: ['25 May, agent: Yavuz (ex-Goeben) in the Golden Horn, Midilli at Stenia'], age: 9000, area: 'Mediterranean' },
  { id: 'c_wolf', kind: 'blob', side: 'DE', title: 'Raider? (Indian Ocean)', at: [-8, 78], r: 900, est: { cap: [0, 0], cru: [0, 1], lt: [0, 0] }, reports: ['22 May, Lloyd\'s: SS Wairuna overdue, last noon position 6°S 80°E', '24 May, rumour (Colombo): "disguised steamer with guns"'], age: 13000, area: 'Indian Ocean' },
  { id: 'c_ub2', kind: 'blob', side: 'DE', title: 'U-boat? (N. Channel)', at: [55.6, -6.9], r: 40, est: { cap: [0, 0], cru: [0, 0], lt: [1, 1] }, reports: ['16:05 trawler Ebor: periscope, wake heading E'], age: 37, area: 'Home waters' },
];

const ENCOUNTERS = [
  { id: 'E1', name: 'Horns Reef', at: BATTLE_O, start: '15:48', r: 60, phase: 'Heavy action · run to the south', rate: 12, own: 'BCF: 6 BC, 4 BB, 14 CL, 26 DD', foe: 'I SG: 5 BC id · 4 CL? · 6+ TB? · heavy ships? to SSE', losses: 'Indefatigable sunk 16:03', bm: 1 },
  { id: 'E2', name: 'Off Abrolhos', at: [-18.1, -37.5], start: '16:20', r: 40, phase: 'Chase · raider turning away', rate: 12, own: 'Kent, Glasgow, Orama', foe: 'Möwe (raider)', losses: '—', bm: 5 },
  { id: 'E3', name: 'OQ-12, W. Approaches', at: [49.57, -12.35], start: '16:36', r: 25, phase: 'U-boat attack · Clan Ross torpedoed', rate: 12, own: '2 sloops, 8 merchants', foe: 'U-boat? (periscope)', losses: 'Clan Ross: hit, settling', bm: 0 },
];

const WRECKS = [
  { name: 'Indefatigable', rel: [5.9, -8.6], at: '16:03', note: 'magazine explosion (X turret) · 2 survivors in the water' },
];

const PORTS = [
  { n: 'Scapa Flow', at: [58.86, -3.10], side: 'RN', big: 1 }, { n: 'Rosyth', at: [56.0215, -3.4440], side: 'RN', big: 1, docks: 3 },
  { n: 'Cromarty', at: [57.69, -4.10], side: 'RN' }, { n: 'Harwich', at: [51.95, 1.29], side: 'RN', big: 1 },
  { n: 'Portsmouth', at: [50.80, -1.11], side: 'RN', big: 1, docks: 15 }, { n: 'Devonport', at: [50.38, -4.18], side: 'RN', docks: 7 },
  { n: 'Chatham', at: [51.40, 0.53], side: 'RN', docks: 9 }, { n: 'Portland', at: [50.565, -2.43], side: 'RN' },
  { n: 'Clydebank', grp: 'Clyde yards', at: [55.9010, -4.4003], side: 'RN', yard: 1 }, { n: 'Govan', grp: 'Clyde yards', at: [55.866, -4.31], side: 'RN', yard: 1 }, { n: 'Dalmuir', grp: 'Clyde yards', at: [55.912, -4.43], side: 'RN', yard: 1 },
  { n: 'Elswick', grp: 'Tyne yards', at: [54.968, -1.655], side: 'RN', yard: 1 }, { n: 'Jarrow', grp: 'Tyne yards', at: [54.983, -1.49], side: 'RN', yard: 1 }, { n: 'Belfast', at: [54.61, -5.905], side: 'RN', yard: 1 },
  { n: 'Dover', at: [51.12, 1.32], side: 'RN' }, { n: 'Malta', at: [35.89, 14.52], side: 'RN', big: 1, docks: 4 }, { n: 'Gibraltar', at: [36.13, -5.35], side: 'RN', docks: 3 },
  { n: 'Mudros', at: [39.865, 25.27], side: 'RN' }, { n: 'Alexandria', at: [31.20, 29.88], side: 'RN' },
  { n: 'Halifax', at: [44.64, -63.56], side: 'RN' }, { n: 'Bermuda', at: [32.32, -64.83], side: 'RN', docks: 1 },
  { n: 'Port Stanley', at: [-51.69, -57.83], side: 'RN' }, { n: "Simon's Town", at: [-34.19, 18.44], side: 'RN', docks: 1 },
  { n: 'Bombay', at: [18.93, 72.84], side: 'RN', docks: 2 }, { n: 'Singapore', at: [1.26, 103.83], side: 'RN' },
  { n: 'Hong Kong', at: [22.29, 114.17], side: 'RN', docks: 1 }, { n: 'Sydney', at: [-33.84, 151.24], side: 'RN', docks: 1 },
  { n: 'Corfu', at: [39.62, 19.92], side: 'FR' }, { n: 'Toulon', at: [43.10, 5.92], side: 'FR' }, { n: 'Brest', at: [48.38, -4.49], side: 'FR' },
  { n: 'Taranto', at: [40.465, 17.22], side: 'IT' }, { n: 'Brindisi', at: [40.64, 17.95], side: 'IT' },
  { n: 'Helsingfors', at: [60.155, 24.96], side: 'RU' }, { n: 'Sevastopol', at: [44.62, 33.52], side: 'RU' },
  { n: 'Kure', at: [34.23, 132.55], side: 'JP' }, { n: 'Hampton Roads', at: [36.97, -76.30], side: 'US' },
  { n: 'Wilhelmshaven', at: [53.52, 8.14], side: 'DE', big: 1 }, { n: 'Kiel', at: [54.33, 10.15], side: 'DE' },
  { n: 'Pola', at: [44.87, 13.84], side: 'AH' }, { n: 'Cattaro', at: [42.43, 18.69], side: 'AH' }, { n: 'Constantinople', at: [41.01, 28.98], side: 'OT' },
];

const SEA_AREAS = [
  { n: 'North Sea', off: [-215, -95], at: [57.2, 2.0], box: [51, -4.5, 61.5, 10] },
  { n: 'Baltic', off: [150, -80], at: [57.4, 19.0], box: [53, 10, 66, 31] },
  { n: 'Home waters W', off: [-270, -30], at: [53.5, -16], box: [45, -25, 61.5, -4.5], label: 'Western Approaches', short: 'W. Approaches' },
  { n: 'North Atlantic', off: [-60, 80], at: [42, -42], box: [20, -80, 65, -25] },
  { n: 'Mediterranean', off: [10, 70], at: [34.0, 19], box: [30, -6, 46, 37] },
  { n: 'South Atlantic', at: [-24, -16], box: [-60, -70, 5, 20] },
  { n: 'Indian Ocean', at: [-14, 72], box: [-45, 20, 30, 100] },
  { n: 'Pacific', at: [12, 152], box: [-50, 100, 60, 180] },
];

const OCEAN_LABELS = [
  ['NORTH  ATLANTIC  OCEAN', 33, -45, 0], ['SOUTH  ATLANTIC  OCEAN', -32, -18, 0], ['INDIAN  OCEAN', -22, 80, 0], ['PACIFIC  OCEAN', 5, -150, 0], ['PACIFIC  OCEAN', 25, 165, 0],
  ['NORTH  SEA', 56.3, 3.2, 1], ['NORWEGIAN  SEA', 66, 2, 1], ['MEDITERRANEAN', 35.2, 18, 1], ['BALTIC', 56.6, 18.4, 1], ['BAY OF BISCAY', 45.4, -5.2, 1], ['SKAGERRAK', 57.9, 8.6, 2], ['HEL. BIGHT', 54.3, 7.6, 2], ['FIRTH OF FORTH', 56.07, -3.0, 3],
];

const DECISIONS = [
  { k: 'dmg', t: '16:41', title: 'Lion: Q turret magazine flooded', body: 'Fire in Q gunhouse out; 3 turrets in action, 320 t water aboard. Captain Chatfield asks: hold station at the head of the line?', acts: ['Hold line', 'Haul out to port'], ref: ['ship', 'Lion'] },
  { k: 'sig', t: '16:40', title: 'New contact: heavy ships? to SSE', body: 'Southampton: "smoke and masts, bearing 148, ~11 nm, many ships". Not identified. Fits the DF fix on DK.', acts: ['Assume it is the HSF', 'Wait for more reports'], ref: ['contact', 'c_smoke'] },
  { k: 'cnv', t: '16:39', title: 'OQ-12: commodore asks to scatter', body: 'Clan Ross torpedoed (port side, settling). Periscope seen by Lavender 1,200 yd off the port column.', acts: ['Keep formation, zig-zag', 'Scatter'], ref: ['unit', 'oq12'] },
];

const LOG = [
  ['16:42', 'gun', 'Tiger: straddle on Moltke (salvo 31)'], ['16:41', 'dmg', 'Lion: Q magazine flooded by order; Q turret out of action'], ['16:41', 'sig', 'Birmingham: large ships, bearing 150'],
  ['16:40', 'gun', 'Derfflinger → Queen Mary: hit (2nd), forecastle'], ['16:39', 'cnv', 'OQ-12: Clan Ross torpedoed, 49°33′N 12°24′W'], ['16:38', 'sig', 'Southampton: smoke and masts bearing 148'],
  ['16:38', 'sig', 'Iron Duke → 5th BS: "course 135" (flags, in transit)'], ['16:36', 'gun', 'Barham opens fire on Von der Tann at 19,000 yd'], ['16:33', 'gun', 'Lützow → Lion: hit, Q turret (fire)'],
  ['16:31', 'cmd', 'You: 1st BCS course 150, 25 kn (direct)'], ['16:24', 'tac', 'Kent sights Möwe, 13 nm; E2 opens · speed held at 12×'], ['16:20', 'int', 'Room 40: DK fixed 55°55′N 6°40′E ±15 nm'],
  ['16:03', 'dmg', 'Indefatigable: magazine explosion, sunk (Von der Tann, 3 hits)'], ['16:00', 'yrd', 'Hood laid down at John Brown, slip 4'], ['15:48', 'tac', 'I SG opens fire, 16,500 yd; E1 opens · speed held at 12×'],
];

// research fields for the Research screen
const RESEARCH = [
  { f: 'Gunnery', share: 18, cur: 'Shell with improved AP cap ("Green Boy")', p: 0.34, eta: 'Mar 1918', items: [[1912, '13.5 in Mk V', 2], [1915, '15 in Mk I', 2], [1917, 'Green Boy AP', 1], [1918, '18 in Mk I', 0], [1920, '16 in/45', 0]], aff: 'All capital ships (refit)' },
  { f: 'Fire control', share: 16, cur: 'Director firing for secondary batteries', p: 0.62, eta: 'Mar 1917', items: [[1912, 'Dreyer table Mk I', 2], [1914, 'Director (main)', 2], [1916, 'Dreyer Mk IV', 2], [1917, 'Secondary director', 1], [1919, 'AFCT', 0]], aff: '23 ships (refit) · designer: Fire control tab' },
  { f: 'Armour', share: 10, cur: 'Thicker deck over magazines (post-action)', p: 0.12, eta: 'Nov 1916', items: [[1905, 'KC armour', 2], [1913, 'Hadfield plate', 2], [1916, 'Magazine deck', 1], [1918, 'Bulges', 0]], aff: 'Battlecruisers first (urgent?)' },
  { f: 'Machinery', share: 12, cur: 'Small-tube boilers', p: 0.78, eta: 'Aug 1916', items: [[1906, 'Turbines', 2], [1912, 'All-oil firing', 2], [1916, 'Small-tube boilers', 1], [1918, 'Geared turbines', 0]], aff: 'designer: Speed & machinery' },
  { f: 'Torpedoes & mines', share: 9, cur: 'H-type mine (copy of German)', p: 0.41, eta: 'Jan 1917', items: [[1909, '21 in Mk II', 2], [1914, '21 in Mk IV', 2], [1917, 'H2 mine', 1], [1918, 'Paravane', 0]], aff: 'Minelayers, destroyers' },
  { f: 'Hull & construction', share: 8, cur: 'Anti-torpedo bulges', p: 0.55, eta: 'Oct 1916', items: [[1906, 'HTS hull', 2], [1915, 'Bulges (monitors)', 2], [1916, 'Bulges (BB)', 1], [1919, 'Welded fittings', 0]], aff: 'Refit: old battleships' },
  { f: 'Signals & W/T', share: 7, cur: 'Short-range W/T for flotillas', p: 0.48, eta: 'Dec 1916', items: [[1910, 'Long-range W/T', 2], [1914, 'Room 40 / DF', 2], [1916, 'Flotilla W/T', 1], [1918, 'Fleet R/T', 0]], aff: 'Signal delay −30% in flotillas' },
  { f: 'Aviation', share: 11, cur: 'Flying-off platforms on turrets', p: 0.27, eta: 'Jun 1917', items: [[1914, 'Seaplane carrier', 2], [1916, 'Turret platform', 1], [1917, 'Flush deck carrier', 0], [1918, 'Torpedo plane', 0]], aff: 'Scouting range +' },
  { f: 'Submarines & ASW', share: 9, cur: 'Hydrophones (towed)', p: 0.66, eta: 'Sep 1916', items: [[1912, 'Overseas sub', 2], [1916, 'Hydrophone', 1], [1917, 'Depth charge D', 0], [1918, 'ASDIC', 0]], aff: 'Convoys, sloops' },
];
