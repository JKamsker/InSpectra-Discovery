const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// Find all opencli.json files with their metadata.json siblings
function walk(dir) {
  let results = [];
  for (const f of fs.readdirSync(dir, {withFileTypes:true})) {
    const p = path.join(dir, f.name);
    if (f.isDirectory()) results.push(...walk(p));
    else if (f.name === 'opencli.json') results.push(p);
  }
  return results;
}

const files = walk('index/packages');
const boxChars = /[\u2500-\u257F\u2580-\u259F\u25A0\u2550-\u256C]/;
let deleted = 0;
let cleaned = 0;

function hasSandboxPath(text) {
  return text && text.includes('/tmp/inspectra-');
}

function hasBoxChars(text) {
  return text && boxChars.test(text);
}

function isGarbageCmd(name) {
  if (!name) return false;
  if (name === '|' || name === '||') return true;
  if (name.startsWith('| ') && name.includes(':')) return true;
  if (hasBoxChars(name)) return true;
  if (/\.cs:line\s+\d+/.test(name)) return true;
  return false;
}

function isErrorTitle(title) {
  if (!title) return false;
  if (/^(Error|Warning)\b/i.test(title)) return true;
  if (/Unhandled exception/i.test(title)) return true;
  if (/fatal error/i.test(title)) return true;
  if (/System\.\w+Exception/.test(title)) return true;
  if (hasSandboxPath(title)) return true;
  if (hasBoxChars(title)) return true;
  return false;
}

function isBadDescription(desc) {
  if (!desc) return false;
  if (hasSandboxPath(desc)) return true;
  if (/System\.\w+Exception/.test(desc)) return true;
  if (hasBoxChars(desc)) return true;
  return false;
}

function hasGarbageCommands(cmds, depth) {
  if (!cmds || depth > 8) return false;
  for (const c of cmds) {
    if (isGarbageCmd(c.name)) return true;
    if (hasGarbageCommands(c.commands, depth + 1)) return true;
  }
  return false;
}

function hasSandboxAnywhere(obj) {
  const json = JSON.stringify(obj);
  return json.includes('/tmp/inspectra-');
}

for (const f of files) {
  const d = JSON.parse(fs.readFileSync(f, 'utf8'));
  const title = d.info?.title || '';
  const desc = d.info?.description || '';

  const shouldDelete = isErrorTitle(title)
    || isBadDescription(desc)
    || hasGarbageCommands(d.commands, 0)
    || hasSandboxAnywhere(d);

  if (shouldDelete) {
    const pkg = f.split(path.sep).slice(-3, -1).join('/');

    // Delete the opencli.json
    fs.unlinkSync(f);
    console.log('DELETED: ' + pkg);

    // Update metadata.json if it exists
    const metadataPath = path.join(path.dirname(f), 'metadata.json');
    if (fs.existsSync(metadataPath)) {
      const metadata = JSON.parse(fs.readFileSync(metadataPath, 'utf8'));
      metadata.status = 'partial';
      if (metadata.introspection?.opencli) {
        metadata.introspection.opencli.status = 'rejected';
        metadata.introspection.opencli.rejectionReason = 'invalid-opencli-artifact';
      }
      if (metadata.artifacts) {
        delete metadata.artifacts.opencliPath;
        delete metadata.artifacts.opencliSource;
      }
      fs.writeFileSync(metadataPath, JSON.stringify(metadata, null, 2) + '\n');
    }

    deleted++;
  }
}

console.log('\nTotal scanned: ' + files.length);
console.log('Deleted: ' + deleted);
