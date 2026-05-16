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

const boxChars = /[\u2500-\u257F\u2580-\u259F\u25A0\u2550-\u256C]/;
const stackTrace = /\bat\s+\S+\.\S+\(|\.cs:line\s+\d+/;
const exceptionType = /System\.\w+Exception\b/;
const sandboxPath = /\/tmp\/inspectra-/i;
const windowsPath = /[A-Z]:\\(?:Users|Program|Windows)\\/;
const linuxSysPath = /\/usr\/share\/dotnet\//;
const ansiEscape = /\x1b\[[\d;]*m/;
const urlPattern = /https?:\/\/\S{80,}/;
const hashBuild = /\+[0-9a-f]{20,}/;
const allHashes = /^[0-9a-f]{32,}$/i;
const bareVersion = /^\d+\.\d+[\d.+\-a-zA-Z]*$/;
const copyrightInTitle = /copyright|\(c\)|\ball rights reserved\b/i;
const errorInTitle = /^(Error|Warning|Fatal)\b|Unhandled exception|Could not (?:load|parse|find)|Failed to/i;
const decorativeLine = /^[#*=─═\-_]{10,}$/;
const logPrefix = /^\[\d{2}:\d{2}:\d{2}\]|\b(?:INF|ERR|WRN|DBG)\b/;

const issues = {};

function addIssue(pkg, category, detail) {
  if (!issues[category]) issues[category] = [];
  issues[category].push({ pkg, detail: (detail || '').substring(0, 120) });
}

function checkString(pkg, field, value) {
  if (!value || typeof value !== 'string') return;
  if (sandboxPath.test(value)) addIssue(pkg, 'SANDBOX_LEAK', `${field}: ${value}`);
  if (ansiEscape.test(value)) addIssue(pkg, 'ANSI_ESCAPE', `${field}: ${value}`);
  if (windowsPath.test(value)) addIssue(pkg, 'WINDOWS_PATH', `${field}: ${value}`);
  if (linuxSysPath.test(value)) addIssue(pkg, 'LINUX_SYS_PATH', `${field}: ${value}`);
}

function checkTitle(pkg, title) {
  if (!title) return;
  if (bareVersion.test(title.trim())) addIssue(pkg, 'BARE_VERSION_TITLE', title);
  if (copyrightInTitle.test(title)) addIssue(pkg, 'COPYRIGHT_TITLE', title);
  if (errorInTitle.test(title)) addIssue(pkg, 'ERROR_TITLE', title);
  if (exceptionType.test(title)) addIssue(pkg, 'EXCEPTION_TITLE', title);
  if (stackTrace.test(title)) addIssue(pkg, 'STACKTRACE_TITLE', title);
  if (boxChars.test(title)) addIssue(pkg, 'BOXCHARS_TITLE', title);
  if (decorativeLine.test(title.trim())) addIssue(pkg, 'DECORATIVE_TITLE', title);
  if (logPrefix.test(title)) addIssue(pkg, 'LOG_PREFIX_TITLE', title);
  if (title.length > 120) addIssue(pkg, 'LONG_TITLE', title);
  if (hashBuild.test(title)) addIssue(pkg, 'HASH_IN_TITLE', title);
}

function checkDescription(pkg, field, desc) {
  if (!desc) return;
  if (exceptionType.test(desc)) addIssue(pkg, 'EXCEPTION_IN_DESC', `${field}: ${desc}`);
  if (stackTrace.test(desc)) addIssue(pkg, 'STACKTRACE_IN_DESC', `${field}: ${desc}`);
  if (boxChars.test(desc)) addIssue(pkg, 'BOXCHARS_IN_DESC', `${field}: ${desc}`);
  if (desc.includes('\x00')) addIssue(pkg, 'NULL_BYTE_IN_DESC', `${field}`);
  checkString(pkg, field, desc);
}

function checkCommandName(pkg, name, cmdPath) {
  if (!name) return;
  if (boxChars.test(name)) addIssue(pkg, 'BOXCHARS_CMD_NAME', `${cmdPath}: ${name}`);
  if (name === '|' || name === '||') addIssue(pkg, 'PIPE_CMD_NAME', `${cmdPath}: ${name}`);
  if (name.startsWith('| ') && name.includes(':')) addIssue(pkg, 'PIPE_ART_CMD', `${cmdPath}: ${name}`);
  if (stackTrace.test(name)) addIssue(pkg, 'STACKTRACE_CMD', `${cmdPath}: ${name}`);
  if (exceptionType.test(name)) addIssue(pkg, 'EXCEPTION_CMD', `${cmdPath}: ${name}`);
  if (decorativeLine.test(name.trim())) addIssue(pkg, 'DECORATIVE_CMD', `${cmdPath}: ${name}`);
  if (name.length > 60) addIssue(pkg, 'LONG_CMD_NAME', `${cmdPath}: ${name}`);
  if (/\s{2,}/.test(name)) addIssue(pkg, 'MULTI_SPACE_CMD', `${cmdPath}: ${name}`);
  if (ansiEscape.test(name)) addIssue(pkg, 'ANSI_CMD_NAME', `${cmdPath}: ${name}`);
  checkString(pkg, `${cmdPath}.name`, name);
}

function checkOptionName(pkg, name, optPath) {
  if (!name) return;
  if (!name.startsWith('-') && !name.startsWith('/')) addIssue(pkg, 'OPTION_NO_PREFIX', `${optPath}: ${name}`);
  if (name.length > 60) addIssue(pkg, 'LONG_OPTION_NAME', `${optPath}: ${name}`);
  if (ansiEscape.test(name)) addIssue(pkg, 'ANSI_OPTION_NAME', `${optPath}: ${name}`);
  if (boxChars.test(name)) addIssue(pkg, 'BOXCHARS_OPTION', `${optPath}: ${name}`);
  if (/\s/.test(name.trim())) addIssue(pkg, 'SPACE_IN_OPTION', `${optPath}: ${name}`);
}

function checkOptions(pkg, options, parentPath) {
  if (!options) return;
  for (let i = 0; i < options.length; i++) {
    const o = options[i];
    const oPath = `${parentPath}.options[${i}]`;
    checkOptionName(pkg, o.name, oPath);
    checkDescription(pkg, `${oPath}.description`, o.description);
    if (o.aliases) {
      for (const a of o.aliases) {
        if (a && !a.startsWith('-') && !a.startsWith('/')) addIssue(pkg, 'ALIAS_NO_PREFIX', `${oPath}.alias: ${a}`);
      }
    }
    if (o.arguments) {
      for (let j = 0; j < o.arguments.length; j++) {
        checkDescription(pkg, `${oPath}.arguments[${j}].description`, o.arguments[j]?.description);
      }
    }
  }
}

function checkArguments(pkg, args, parentPath) {
  if (!args) return;
  for (let i = 0; i < args.length; i++) {
    checkDescription(pkg, `${parentPath}.arguments[${i}].description`, args[i]?.description);
  }
}

function checkCommands(pkg, cmds, parentPath, depth) {
  if (!cmds || depth > 10) return;
  for (let i = 0; i < cmds.length; i++) {
    const c = cmds[i];
    const cPath = `${parentPath}.commands[${i}]`;
    checkCommandName(pkg, c.name, cPath);
    checkDescription(pkg, `${cPath}.description`, c.description);
    checkOptions(pkg, c.options, cPath);
    checkArguments(pkg, c.arguments, cPath);
    checkCommands(pkg, c.commands, cPath, depth + 1);
  }
}

const files = walk('index/packages');
for (const f of files) {
  const d = JSON.parse(fs.readFileSync(f, 'utf8'));
  const pkg = f.split(path.sep).slice(-3, -1).join('/');

  // Info checks
  checkTitle(pkg, d.info?.title);
  checkDescription(pkg, 'info.description', d.info?.description);

  // Root options/arguments
  checkOptions(pkg, d.options, '$');
  checkArguments(pkg, d.arguments, '$');

  // Commands tree
  checkCommands(pkg, d.commands, '$', 0);
}

// Print summary
const cats = Object.keys(issues).sort((a, b) => issues[b].length - issues[a].length);
console.log(`Scanned ${files.length} files\n`);
console.log('=== ISSUE SUMMARY ===');
for (const cat of cats) {
  console.log(`  ${cat}: ${issues[cat].length}`);
}

// Print details
for (const cat of cats) {
  const items = issues[cat];
  console.log(`\n=== ${cat} (${items.length}) ===`);
  // Dedupe by pkg
  const seen = new Set();
  for (const item of items) {
    const key = `${item.pkg}|${item.detail}`;
    if (seen.has(key)) continue;
    seen.add(key);
    console.log(`  ${item.pkg}: ${item.detail}`);
    if (seen.size >= 15) { // cap per category
      if (items.length > 15) console.log(`  ... and ${items.length - 15} more`);
      break;
    }
  }
}
