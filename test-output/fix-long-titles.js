const fs = require('fs'), path = require('path');
function walk(dir) {
  let r = [];
  for (const f of fs.readdirSync(dir, {withFileTypes:true})) {
    const p = path.join(dir, f.name);
    if (f.isDirectory() && f.name !== 'latest') r.push(...walk(p));
    else if (f.name === 'opencli.json') r.push(p);
  }
  return r;
}

// Strip version+hash from titles like "Tool Name (1.2.3+abc123..."
// Strip "Running on ..." preambles
// Strip "Command-line v1.2.3+hash: ..."
function cleanTitle(t) {
  if (!t) return t;
  let s = t;
  // "Running on linux-x64, downloading tailwindcss cli v4.2.2+..." → "tailwindcss cli"
  s = s.replace(/^Running on\s+[^,]+,\s*downloading\s+/i, '');
  // "Command-line v1.2.3+hash: <path> -h" → "Command-line"
  s = s.replace(/\s+v?\d+\.\d+[\d.]*\+[0-9a-f]{8,}[^)]*$/i, '');
  // "Tool Name (1.2.3+abc123..." → "Tool Name"
  s = s.replace(/\s*\(\d+[\d.]*\+[0-9a-f]{8,}[^)]*\)?\s*$/, '');
  // "Tool Name (1.2.3)" → "Tool Name"
  s = s.replace(/\s*\(\d+[\d.]+\)\s*$/, '');
  // Strip trailing "using nuget..." etc
  s = s.replace(/\s+using\s+nuget.*$/i, '');
  // Strip trailing version like " v4.2.2+697e..."
  s = s.replace(/\s+v?\d+\.\d+[\d.]*\+[0-9a-f]{8,}.*$/i, '');
  // Trim
  return s.trim();
}

let fixed = 0;
for (const f of walk('index/packages')) {
  const d = JSON.parse(fs.readFileSync(f, 'utf8'));
  const src = d['x-inspectra']?.artifactSource || '';
  if (src === 'tool-output') continue;
  const t = d.info?.title;
  if (!t || t.length < 60) continue;

  const cleaned = cleanTitle(t);
  if (cleaned !== t && cleaned.length > 0 && cleaned.length < t.length) {
    d.info.title = cleaned;
    fs.writeFileSync(f, JSON.stringify(d, null, 2));
    const pkg = f.split(path.sep).slice(-3, -1).join('/');
    console.log('FIXED:', pkg);
    console.log('  FROM:', t.substring(0, 80));
    console.log('    TO:', cleaned);
    fixed++;
  }
}
console.log('\nFixed:', fixed);
