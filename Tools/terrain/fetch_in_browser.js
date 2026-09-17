// How the two source rasters were fetched (17.09.2026). The cloud sandbox cannot reach the data buckets, so this runs in a browser tab
// opened on the bucket's own origin (same-origin, no CORS), then downloads the result.
// 1) open https://pgc-opendata-dems.s3.us-west-2.amazonaws.com/arcticdem/mosaics/v4.1/2m/48_71/48_71_2_1_2m_v4.1_meta.txt  → await fetchDem()
// 2) open https://dataforgood-fb-data.s3.amazonaws.com/?prefix=forests/v1/alsgedi_global_v6_float/chm/121012322         → await fetchCanopy()
// Tile ids: PGC STAC https://stac.pgc.umn.edu/api/v1/collections/arcticdem-mosaics-v4.1-2m/items?bbox=59.40,61.74,59.48,61.78
// and the zoom-9 quadkey of the area centre for the Meta/WRI canopy model.
const A = 6378137, F = 1 / 298.257223563, E2 = F * (2 - F), E = Math.sqrt(E2), D = Math.PI / 180;
const LAT0 = 61.756, LON0 = 59.4425, N = 2049, STEP = 2, HALF = 2048;
const M0 = A * (1 - E2) / Math.pow(1 - E2 * Math.sin(LAT0 * D) ** 2, 1.5), Nr = lat => A / Math.sqrt(1 - E2 * Math.sin(lat * D) ** 2);
// x = east, z = SOUTH in this script (row 0 = northern edge); package.py flips rows for Unity (row 0 = south).
const toLL = (x, z) => { const lat = LAT0 - z / M0 / D; return [lat, LON0 + x / (Nr(lat) * Math.cos(lat * D)) / D]; };
const tfn = p => Math.tan(Math.PI / 4 - p / 2) / Math.pow((1 - E * Math.sin(p)) / (1 + E * Math.sin(p)), E / 2);
const tc = tfn(70 * D), mc = Math.cos(70 * D) / Math.sqrt(1 - E2 * Math.sin(70 * D) ** 2);
// EPSG:3413 (NSIDC polar stereographic north, true scale at 70° N, central meridian −45°)
const ps = (lat, lon) => { const r = A * mc * tfn(lat * D) / tc, l = (lon + 45) * D; return [r * Math.sin(l), -r * Math.cos(l)]; };
const save = (data, name) => {
  const a = document.createElementNS('http://www.w3.org/1999/xhtml', 'a');
  a.href = URL.createObjectURL(new Blob([data])); a.download = name;
  (document.body || document.documentElement).append(a); a.click();
};

async function fetchDem() {
  const G = await import('https://cdn.jsdelivr.net/npm/geotiff@2.1.3/+esm');
  const im = await (await G.fromUrl(location.origin + '/arcticdem/mosaics/v4.1/2m/48_71/48_71_2_1_2m_v4.1_dem.tif')).getImage();
  const [bx0, , , by1] = im.getBoundingBox(), xs = [], ys = [];
  for (const [x, z] of [[-HALF, -HALF], [HALF, -HALF], [-HALF, HALF], [HALF, HALF], [0, -HALF], [0, HALF], [-HALF, 0], [HALF, 0]]) {
    const [px, py] = ps(...toLL(x, z)); xs.push(px); ys.push(py);
  }
  const c0 = Math.floor((Math.min(...xs) - bx0) / 2) - 2, c1 = Math.ceil((Math.max(...xs) - bx0) / 2) + 2;
  const r0 = Math.floor((by1 - Math.max(...ys)) / 2) - 2, r1 = Math.ceil((by1 - Math.min(...ys)) / 2) + 2;
  const [ras] = await im.readRasters({ window: [c0, r0, c1, r1] }), W = c1 - c0, out = new Float32Array(N * N);
  for (let j = 0; j < N; j++) for (let i = 0; i < N; i++) {
    const [px, py] = ps(...toLL(-HALF + i * STEP, -HALF + j * STEP));
    const fx = (px - bx0) / 2 - .5 - c0, fy = (by1 - py) / 2 - .5 - r0, ix = Math.floor(fx), iy = Math.floor(fy), u = fx - ix, v = fy - iy;
    out[j * N + i] = (ras[iy * W + ix] * (1 - u) + ras[iy * W + ix + 1] * u) * (1 - v) + (ras[(iy + 1) * W + ix] * (1 - u) + ras[(iy + 1) * W + ix + 1] * u) * v;
  }
  save(out.buffer, '1079-terrain-2m-dem-f32.bin');
}

async function fetchCanopy() {
  const G = await import('https://cdn.jsdelivr.net/npm/geotiff@2.1.3/+esm');
  const im = await (await G.fromUrl(location.origin + '/forests/v1/alsgedi_global_v6_float/chm/121012322.tif')).getImage();
  const [bx0, , , by1] = im.getBoundingBox(), res = im.getResolution()[0], out = new Uint8Array(N * N), rows = [...Array(N).keys()];
  async function work() {
    while (rows.length) {
      const j = rows.shift(), lat = LAT0 - (-HALF + j * STEP) / M0 / D;
      const py = Math.round((by1 - A * Math.asinh(Math.tan(lat * D))) / res - .5), k = Nr(lat) * Math.cos(lat * D);
      const pxs = Array.from({ length: N }, (_, i) => Math.floor(((LON0 + (-HALF + i * STEP) / k / D) * D * A - bx0) / res));
      const [r] = await im.readRasters({ window: [pxs[0], py, pxs[N - 1] + 1, py + 1] });
      for (let i = 0; i < N; i++) out[j * N + i] = r[pxs[i] - pxs[0]];
    }
  }
  await Promise.all(Array.from({ length: 16 }, work));
  save(out, '1079-terrain-2m-chm-u8.bin');
}
