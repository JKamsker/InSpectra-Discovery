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

const sandboxPath = /\/tmp\/inspectra-/i;
const stackTrace = /\bat\s+\S+\.\S+\(|\.cs:line\s+\d+/;
const exceptionType = /System\.\w+Exception\b/;
const logPrefix = /^\[\d{2}:\d{2}:\d{2}\s+\w+\]/;
const decorativeLine = /^[#*=─═\-_]{10,}$/;
const hashInTitle = /\+[0-9a-f]{20,}/i;

function scrubSandboxPaths(text) {
  return text.replace(/\/tmp\/inspectra-[^\s"'\]}>)]+/gi, '<path>');
}

let deletedFiles = 0, fixedTitles = 0, fixedDescs = 0, fixedOptions = 0;

for (const f of walk('index/packages')) {
  const raw = fs.readFileSync(f, 'utf8');
  const d = JSON.parse(raw);
  const pkg = f.split(path.sep).slice(-3, -1).join('/');
  let changed = false;
  let shouldDelete = false;

  // Fix title
  const title = d.info?.title;
  if (title) {
    if (logPrefix.test(title) || decorativeLine.test(title.trim())) {
      delete d.info.title;
      changed = true; fixedTitles++;
    } else if (hashInTitle.test(title)) {
      d.info.title = title.replace(/\s*\(?\+[0-9a-f]{20,}[^)]*\)?/gi, '').replace(/\s+/g, ' ').trim();
      if (!d.info.title) delete d.info.title;
      changed = true; fixedTitles++;
    }
  }

  // Fix info.description - delete if it's full of exceptions/stack traces
  const desc = d.info?.description;
  if (desc) {
    if (exceptionType.test(desc) && stackTrace.test(desc)) {
      // Full stack trace in description - delete the whole artifact
      shouldDelete = true;
    } else if (sandboxPath.test(desc)) {
      d.info.description = scrubSandboxPaths(desc);
      if (d.info.description === '<path>' || !d.info.description.trim()) delete d.info.description;
      changed = true; fixedDescs++;
    }
  }

  // Fix option/argument descriptions with stack traces or sandbox paths
  function fixNode(node) {
    if (!node) return;
    if (Array.isArray(node)) { node.forEach(fixNode); return; }
    if (typeof node !== 'object') return;

    if (node.description && typeof node.description === 'string') {
      if (stackTrace.test(node.description) && exceptionType.test(node.description)) {
        delete node.description;
        changed = true; fixedOptions++;
      } else if (stackTrace.test(node.description) && node.description.indexOf('\nat ') > -1) {
        delete node.description;
        changed = true; fixedOptions++;
      } else if (sandboxPath.test(node.description)) {
        node.description = scrubSandboxPaths(node.description);
        changed = true; fixedOptions++;
      }
    }
    if (node.options) fixNode(node.options);
    if (node.arguments) fixNode(node.arguments);
    if (node.commands) fixNode(node.commands);
  }

  if (!shouldDelete) {
    fixNode(d.options);
    fixNode(d.arguments);
    fixNode(d.commands);
  }

  if (shouldDelete) {
    fs.unlinkSync(f);
    const metaPath = path.join(path.dirname(f), 'metadata.json');
    if (fs.existsSync(metaPath)) {
      const m = JSON.parse(fs.readFileSync(metaPath, 'utf8'));
      m.status = 'partial';
      if (m.introspection?.opencli) {
        m.introspection.opencli.status = 'rejected';
        m.introspection.opencli.rejectionReason = 'invalid-opencli-artifact';
      }
      if (m.artifacts) { delete m.artifacts.opencliPath; delete m.artifacts.opencliSource; }
      fs.writeFileSync(metaPath, JSON.stringify(m, null, 2) + '\n');
    }
    console.log('DELETED:', pkg);
    deletedFiles++;
  } else if (changed) {
    fs.writeFileSync(f, JSON.stringify(d, null, 2));
    console.log('FIXED:', pkg);
  }
}

console.log('\n--- Summary ---');
console.log('Deleted:', deletedFiles);
console.log('Fixed titles:', fixedTitles);
console.log('Fixed descriptions:', fixedDescs);
console.log('Fixed option/arg descriptions:', fixedOptions);
