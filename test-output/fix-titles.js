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
const bareVersion = /^\d+\.\d+[\d.+\-a-zA-Z]*$/;
const copyrightPattern = /copyright|\(c\)|all rights reserved/i;
let fixed = 0;
for (const f of walk('index/packages')) {
  const d = JSON.parse(fs.readFileSync(f, 'utf8'));
  const t = d.info?.title;
  if (!t) continue;
  const isBad = copyrightPattern.test(t) || bareVersion.test(t.trim());
  if (!isBad) continue;

  delete d.info.title;
  fs.writeFileSync(f, JSON.stringify(d, null, 2));

  const dir = path.dirname(f);
  const latestDir = path.join(path.dirname(dir), 'latest');
  const latestPath = path.join(latestDir, 'opencli.json');
  if (fs.existsSync(latestPath)) {
    const ld = JSON.parse(fs.readFileSync(latestPath, 'utf8'));
    if (ld.info?.title && (copyrightPattern.test(ld.info.title) || bareVersion.test(ld.info.title.trim()))) {
      delete ld.info.title;
      fs.writeFileSync(latestPath, JSON.stringify(ld, null, 2));
    }
  }

  const pkg = f.split(path.sep).slice(-3, -1).join('/');
  console.log('FIXED:', pkg, '- removed:', t.substring(0, 60));
  fixed++;
}
console.log('\nFixed:', fixed);
