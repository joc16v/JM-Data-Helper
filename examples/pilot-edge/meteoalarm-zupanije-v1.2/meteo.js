(function () {
  'use strict';
  const cfg = window.METEO_CONFIG;
  const pl = window.vizrt && window.vizrt.payloadhosting;
  const state = { ready: false, loading: false, controller: null, request: 0, lastLoad: '' };
  const colors = ['#515862', '#29af59', '#f1d124', '#ee8c23', '#d63c51'];
  const colorNames = ['0 · Bez boje', '1 · Zelena', '2 · Žuta', '3 · Narančasta', '4 · Crvena'];
  const symbols = ['Bez simbola', 'Kiša', 'Grmljavina', 'Vjetar', 'Hladnoća', 'Vrućina', 'Magla', 'Snijeg / led'];
  const controls = new Map();
  const byId = id => document.getElementById(id);
  function status(text, kind) { byId('status').textContent = text; byId('status').className = kind || ''; }
  function exists(id) { return !!(state.ready && pl.fieldExists(id)); }
  function select(id, options, label) {
    const el = document.createElement('select'); el.dataset.field = id; el.id = 'field_' + id; el.setAttribute('aria-label', label);
    options.forEach((text, index) => { const option = document.createElement('option'); option.value = String(index); option.textContent = text; el.appendChild(option); });
    return el;
  }
  function colorRow(item, parent) {
    const row = document.createElement('div'); row.className = 'color-row'; row.dataset.region = item.id;
    const swatch = document.createElement('span'); swatch.className = 'swatch'; swatch.setAttribute('aria-hidden', 'true');
    const label = document.createElement('label'); label.textContent = item.name; label.htmlFor = 'field_' + item.field;
    const input = select(item.field, colorNames, 'Boja: ' + item.name);
    row.append(swatch, label, input); parent.appendChild(row);
  }
  cfg.counties.forEach(item => colorRow(item, byId('counties')));
  cfg.sea.forEach(item => colorRow(item, byId('sea')));
  cfg.icons.forEach(city => {
    const card = document.createElement('div'); card.className = 'icon-card';
    const heading = document.createElement('h3'); heading.textContent = city.name; card.appendChild(heading);
    city.slots.forEach((slot, index) => {
      const row = document.createElement('div'); row.className = 'icon-row';
      const label = document.createElement('label'); label.textContent = String(index + 1) + '.';
      row.append(label, select(slot.visible, ['Off', 'On'], city.name + ': prikaz ikone ' + (index + 1)), select(slot.symbol, symbols, city.name + ': simbol ' + (index + 1)), select(slot.color, colorNames, city.name + ': boja ikone ' + (index + 1)));
      card.appendChild(row);
    }); byId('icons').appendChild(card);
  });
  for (let n = 1; n <= 7; n++) {
    const fig = document.createElement('figure'); const img = document.createElement('img'); img.src = n + '.png'; img.alt = '';
    const caption = document.createElement('figcaption'); caption.textContent = n + ' · ' + symbols[n]; fig.append(img, caption); byId('icon-legend').appendChild(fig);
  }
  document.querySelectorAll('[data-field]').forEach(el => { controls.set(el.dataset.field, el); });
  function refreshField(id, value) {
    const el = controls.get(id); if (!el) return;
    if (el.tagName === 'SELECT') {
      Array.from(el.options).filter(o => o.dataset.unknown).forEach(o => o.remove());
      if (value !== null && value !== '' && !Array.from(el.options).some(o => o.value === String(value))) {
        const o = document.createElement('option'); o.value = String(value); o.textContent = 'Vrijednost ' + value; o.dataset.unknown = 'true'; el.appendChild(o);
      }
    }
    el.value = value == null ? '' : String(value);
    const row = el.closest('.color-row'); if (row) row.querySelector('.swatch').style.backgroundColor = colors[Number(value)] || colors[0];
  }
  function updateAvailability() {
    if (!state.ready) return;
    controls.forEach((el, id) => { el.disabled = !exists(id) || state.loading; const row = el.closest('.color-row'); if (row) row.classList.toggle('missing', !exists(id)); });
    const missing = cfg.counties.filter(c => !exists(c.field)).map(c => c.field);
    const absentSea = cfg.sea.filter(c => !exists(c.field)).map(c => c.field);
    const warn = byId('binding-warning'); warn.hidden = !missing.length && !absentSea.length;
    warn.textContent = (missing.length ? 'U Pilot templateu nedostaju županijski fieldovi: ' + missing.join(', ') + '. Osvježi polja iz scene. ' : '') + (absentSea.length ? 'Nedostaju morski fieldovi: ' + absentSea.join(', ') + '.' : '');
    byId('load').disabled = state.loading || missing.length > 0;
    byId('day').disabled = state.loading; byId('include-sea').disabled = state.loading;
  }
  function syncPayload() {
    if (state.loading && state.controller) state.controller.abort();
    controls.forEach((el, id) => {
      if (exists(id)) {
        const value = pl.getFieldText(id);
        const row = el.closest('.color-row');
        if (row && el.value !== String(value == null ? '' : value)) row.classList.remove('modified');
        refreshField(id, value);
      }
    }); updateAvailability();
  }
  function write(updates) {
    updates.forEach(u => { if (!exists(u.field)) throw new Error('U Pilot templateu nedostaje field ' + u.field + '.'); });
    pl.updatePayload(function () { updates.forEach(u => pl.setFieldText(u.field, String(u.value))); return false; });
    updates.forEach(u => refreshField(u.field, String(u.value)));
  }
  controls.forEach((el, id) => {
    el.addEventListener(el.tagName === 'SELECT' ? 'change' : 'input', function () {
      if (!exists(id) || state.loading) return;
      try { write([{field:id, value:el.value}]); const row = el.closest('.color-row'); if (row) row.classList.add('modified'); }
      catch (error) { syncPayload(); status(error.message, 'error'); }
    });
  });
  function regions(array, mapping, label, allowNone = false) {
    if (!Array.isArray(array)) throw new Error('Helper nije vratio popis ' + label + '.');
    const lookup = new Map();
    array.forEach(region => {
      if (!region || typeof region.id !== 'string') throw new Error('Neispravan zapis za ' + label + '.');
      const id = region.id.toLowerCase(); if (lookup.has(id)) throw new Error('Dupli DHMZ ID: ' + id);
      lookup.set(id, region);
    });
    return mapping.map(item => {
      const region = lookup.get(item.id); if (!region) throw new Error('DHMZ podaci nisu potpuni: ' + item.name + '.');
      if (!Number.isInteger(region.level) || region.level < (allowNone ? 0 : 1) || region.level > 4) throw new Error('Neispravna razina upozorenja: ' + item.name + '.');
      return {field:item.field, value:region.level};
    });
  }
  async function loadWarnings() {
    if (!state.ready || state.loading) return;
    const day = Number(byId('day').value); const includeSea = byId('include-sea').checked;
    const token = ++state.request; const controller = new AbortController(); state.controller = controller;
    state.loading = true; updateAvailability(); byId('load').textContent = 'UČITAVAM…';
    status('Dohvaćam DHMZ za ' + byId('day').selectedOptions[0].text.toLowerCase() + '…');
    const timer = setTimeout(() => controller.abort(), 22000);
    try {
      const response = await fetch(cfg.helperUrl + '/api/dhmz/warnings?day=' + day + '&refresh=true', {cache:'no-store', signal:controller.signal});
      const data = await response.json();
      if (!response.ok || !data || data.ok !== true) throw new Error(data && data.error || 'Helper je vratio grešku (' + response.status + ').');
      if (data.day !== day) throw new Error('Helper je vratio podatke za pogrešan dan.');
      const updates = regions(data.counties, cfg.counties, 'županija');
      if (includeSea) updates.push(...regions(data.sea, cfg.sea, 'morskih područja', true));
      if (token !== state.request) return;
      write(updates);
      document.querySelectorAll('#counties .modified').forEach(row => row.classList.remove('modified'));
      if (includeSea) document.querySelectorAll('#sea .modified').forEach(row => row.classList.remove('modified'));
      let time = ''; const fetched = new Date(data.fetchedAt); if (!isNaN(fetched)) time = ' · dohvat ' + fetched.toLocaleTimeString('hr-HR', {hour:'2-digit', minute:'2-digit', timeZone:'Europe/Zagreb'});
      state.lastLoad = 'Učitano 21 županija' + (includeSea ? ' i 6 morskih područja' : '') + ' za ' + byId('day').selectedOptions[0].text.toLowerCase() + time + '. Ručne izmjene su dostupne.';
      status(state.lastLoad, 'success');
    } catch (error) {
      status(error.name === 'AbortError' ? 'Dohvat je istekao ili je zapis promijenjen. Ponovi učitavanje.' : (error instanceof TypeError ? 'JM Data Helper nije dostupan. Pokreni ga na ovom računalu i provjeri vezu.' : error.message), 'error');
    } finally {
      clearTimeout(timer); if (token === state.request) { state.loading = false; state.controller = null; byId('load').textContent = 'UČITAJ DHMZ'; updateAvailability(); }
    }
  }
  byId('load').addEventListener('click', loadWarnings);
  byId('day').addEventListener('change', () => status('Odabran novi dan. Klikni UČITAJ DHMZ da promijeniš boje; trenutačne boje još su iz prethodnog unosa.'));
  if (!pl) { status('Nedostaje payloadhosting.js. Kopiraj sve datoteke iz paketa u isti folder.', 'error'); return; }
  pl.setUsesAutomaticBindings(false);
  pl.addEventListener('payloadchange', syncPayload);
  pl.initialize(function () {
    state.ready = true; byId('fields').disabled = false;
    byId('connection').textContent = 'Pilot povezan'; byId('connection').classList.add('ready'); syncPayload();
    status('Spremno. Pokreni helper, odaberi dan i učitaj DHMZ ili ručno promijeni boje.');
  });
})();
