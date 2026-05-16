const fs = require('fs');
const path = require('path');

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
let issues = 0;

const boxChars = /[\u2500-\u257F\u2580-\u259F\u25A0\u2550-\u256C]/;

for (const f of files) {
  const d = JSON.parse(fs.readFileSync(f, 'utf8'));
  const problems = [];

  const title = d.info?.title || '';
  const desc = d.info?.description || '';

  if (title.includes('/tmp/inspectra-')) problems.push('SANDBOX_TITLE');
  if (desc.includes('/tmp/inspectra-')) problems.push('SANDBOX_DESC');

  if (boxChars.test(title)) problems.push('BOX_TITLE');

  function checkCmds(cmds, depth) {
    if (!cmds || depth > 8) return;
    for (const c of cmds) {
      const n = c.name || '';
      if (boxChars.test(n)) problems.push('BOX_CMD:' + n.substring(0, 30));
      if (n === '|' || n === '||' || (n.startsWith('| ') && n.includes(':'))) problems.push('PIPE_CMD:' + n.substring(0, 30));
      if (/\.cs:line\s+\d+/.test(n)) problems.push('STACKTRACE_CMD');
      if ((c.description || '').includes('/tmp/inspectra-')) problems.push('SANDBOX_CMD_DESC');
      checkCmds(c.commands, depth + 1);
      for (const o of (c.options || [])) {
        if ((o.description || '').includes('/tmp/inspectra-')) problems.push('SANDBOX_OPT');
      }
    }
  }
  checkCmds(d.commands, 0);
  for (const o of (d.options || [])) {
    if ((o.description || '').includes('/tmp/inspectra-')) problems.push('SANDBOX_ROOT_OPT');
  }

  if (/^(Error|Warning)\b|Unhandled exception|fatal error/i.test(title)) problems.push('ERROR_TITLE:' + title.substring(0, 60));
  if (/System\.\w+Exception/.test(title)) problems.push('EXCEPTION_TITLE');
  if (/System\.\w+Exception/.test(desc)) problems.push('EXCEPTION_DESC');

  if (problems.length > 0) {
    const pkg = f.split(path.sep).slice(-3, -1).join('/');
    console.log(pkg + ': ' + [...new Set(problems)].join(', '));
    issues++;
  }
}
console.log('\nTotal files scanned: ' + files.length);
console.log('Files with issues: ' + issues);
