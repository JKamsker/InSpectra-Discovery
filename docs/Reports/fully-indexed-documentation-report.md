# Fully Indexed Package Documentation Report

Generated: 2026-04-02 04:03:07+00:00

Scope: latest package entries with status ok, whose OpenCLI classification is json-ready or json-ready-with-nonzero-exit, and whose resolved OpenCLI provenance is tool-output.

Completeness rule: visible commands, options, and arguments must all have non-empty descriptions, and every visible leaf command must have at least one non-empty example.

Hidden commands, options, and arguments are excluded from the score.

Packages in scope: 20

Fully documented: 10

Incomplete: 10

| Package | Version | Status | XML | Cmd Docs | Opt Docs | Arg Docs | Leaf Examples | Overall |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| [BellaBaxter.Cli](#pkg-bellabaxter-cli) | 0.1.1-preview.36 | ok | xml-ready | 71/71 | 51/174 | 13/30 | 32/59 | FAIL |
| [CloudAwesome.FolkTune.Reviewer](#pkg-cloudawesome-folktune-reviewer) | 2026.4.1.1635 | ok | xml-ready | 6/6 | 31/31 | 0/0 | 0/5 | FAIL |
| [cute](#pkg-cute) | 2.15.0 | ok | requires-interactive-authentication | 36/36 | 413/413 | 0/0 | 0/31 | FAIL |
| [DiffLog](#pkg-difflog) | 0.0.2 | ok | requires-configuration | 3/3 | 24/24 | 0/0 | 2/3 | FAIL |
| [InSpectra.Gen](#pkg-inspectra-gen) | 0.0.46 | ok | xml-ready | 8/8 | 88/88 | 4/4 | 0/5 | FAIL |
| [Karls.Gitflow.Tool](#pkg-karls-gitflow-tool) | 0.0.13 | ok | xml-ready | 41/41 | 0/49 | 2/31 | 0/35 | FAIL |
| [Nexudus.Cli](#pkg-nexudus-cli) | 0.1.5 | ok | xml-ready | 253/253 | 2195/2348 | 58/163 | 0/211 | FAIL |
| [PhoenixCode.Lens](#pkg-phoenixcode-lens) | 1.4.2 | ok | requires-configuration | 7/7 | 18/18 | 0/0 | 0/7 | FAIL |
| [PptMcp.CLI](#pkg-pptmcp-cli) | 1.0.3 | ok | xml-ready | 48/48 | 350/350 | 35/35 | 0/45 | FAIL |
| [TerevintoSoftware.StaticSiteGenerator](#pkg-terevintosoftware-staticsitegenerator) | 10.0.1 | ok | requires-configuration | 1/1 | 9/9 | 1/1 | 0/1 | FAIL |
| [atc-kusto](#pkg-atc-kusto) | 3.4.0 | ok | xml-ready | 21/21 | 75/75 | 9/9 | 17/17 | PASS |
| [atc-rest-api-gen](#pkg-atc-rest-api-gen) | 1.0.137 | ok | xml-ready | 14/14 | 89/89 | 0/0 | 10/10 | PASS |
| [BlazorLocalization.Extractor](#pkg-blazorlocalization-extractor) | 10.1.3 | ok | xml-ready | 2/2 | 12/12 | 2/2 | 2/2 | PASS |
| [claudomat](#pkg-claudomat) | 2026.4.1.30 | ok | xml-ready | 3/3 | 44/44 | 3/3 | 3/3 | PASS |
| [CleanCli](#pkg-cleancli) | 1.0.2 | ok | xml-ready | 1/1 | 0/0 | 0/0 | 1/1 | PASS |
| [DeadCode](#pkg-deadcode) | 1.0.2 | ok | xml-ready | 4/4 | 15/15 | 2/2 | 4/4 | PASS |
| [dotNetTips.Spargine.Dev.Tool](#pkg-dotnettips-spargine-dev-tool) | 2026.10.2.16 | ok | invalid-xml | 2/2 | 12/12 | 0/0 | 2/2 | PASS |
| [DotnetTokenKiller](#pkg-dotnettokenkiller) | 0.5.0 | ok | xml-ready | 21/21 | 36/36 | 8/8 | 18/18 | PASS |
| [GeoMapCli](#pkg-geomapcli) | 0.0.8-beta | ok | xml-ready | 1/1 | 3/3 | 0/0 | 1/1 | PASS |
| [ThunderPipe](#pkg-thunderpipe) | 1.0.2 | ok | xml-ready | 10/10 | 24/24 | 13/13 | 7/7 | PASS |

## Package Details

<a id="pkg-bellabaxter-cli"></a>
### BellaBaxter.Cli

- Version: `0.1.1-preview.36`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `71/71`
- Option documentation: `51/174`
- Argument documentation: `13/30`
- Leaf command examples: `32/59`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: environments create --description, environments create --json, environments create --name, environments create --project, environments delete --force, environments delete --json, environments delete --project, environments get --json, environments get --project, environments list --json, environments list --project, environments update --description, environments update --json, environments update --name, environments update --project, login --api-key, login --force, login --json, logout --force, projects create --description, projects create --json, projects create --name, projects delete --force, projects delete --json, projects get --json, projects list --json, projects list --page, projects list --size, projects list --sort-by, projects list --sort-dir, projects update --description, projects update --json, projects update --name, providers create --description, providers create --json, providers create --name, providers create --type, providers delete --force, providers delete --json, providers get --json, providers list --json, providers list --project, pull --app, pull --environment, pull --format, pull --json, pull --output, pull --project, pull --provider, secrets delete --environment, secrets delete --force, secrets delete --json, secrets delete --project, secrets generate --class-name, secrets generate --dry-run, secrets generate --environment, secrets generate --namespace, secrets generate --output, secrets generate --project, secrets get --app, secrets get --environment, secrets get --format, secrets get --json, secrets get --output, secrets get --project, secrets get --provider, secrets list --environment, secrets list --json, secrets list --project, secrets push --description, secrets push --environment, secrets push --input, secrets push --json, secrets push --project, secrets set --description, secrets set --environment, secrets set --json, secrets set --project, ssh ca-key --environment, ssh ca-key --json, ssh ca-key --output, ssh ca-key --project, ssh configure --environment, ssh configure --json, ssh configure --project, ssh connect --environment, ssh connect --json, ssh connect --project, ssh connect --role, ssh roles create --environment, ssh roles create --json, ssh roles create --name, ssh roles create --project, ssh roles delete --environment, ssh roles delete --json, ssh roles delete --name, ssh roles delete --project, ssh roles list --environment, ssh roles list --json, ssh roles list --project, ssh sign --environment, ssh sign --json, ssh sign --project, ssh sign --role, totp code --environment, totp code --json, totp code --project, totp code --quiet, totp delete --environment, totp delete --force, totp delete --json, totp delete --project, totp generate --account, totp generate --environment, totp generate --issuer, totp generate --json, totp generate --project, totp import --environment, totp import --json, totp import --project, totp list --environment, totp list --json, totp list --project
- Missing argument descriptions: environments delete <slug>, environments get <slug>, environments update <slug>, projects delete <identifier>, projects get <identifier>, projects update <identifier>, providers delete <id>, providers get <id-or-name>, secrets delete <key>, secrets generate <language>, secrets set <key>, secrets set <value>, totp code <name>, totp delete <name>, totp generate <name>, totp import <name>, totp import <otpauth-url>
- Missing leaf command examples: auth refresh, auth status, config set-server, config show, context clear, context show, environments create, environments delete, environments get, environments list, environments update, logout, projects create, projects delete, projects get, projects list, projects update, providers create, providers delete, providers get, providers list, secrets delete, secrets get, secrets list, secrets push, secrets set, whoami

<a id="pkg-cloudawesome-folktune-reviewer"></a>
### CloudAwesome.FolkTune.Reviewer

- Version: `2026.4.1.1635`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `6/6`
- Option documentation: `31/31`
- Argument documentation: `0/0`
- Leaf command examples: `0/5`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: admin id-init, pick, review, session, stats

<a id="pkg-cute"></a>
### cute

- Version: `2.15.0`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `requires-interactive-authentication`
- Command documentation: `36/36`
- Option documentation: `413/413`
- Argument documentation: `0/0`
- Leaf command examples: `0/31`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: app generate, chat, content delete, content download, content edit, content generate, content generate-test, content join, content publish, content replace, content set-default, content sync-api, content translate, content unpublish, content upload, eval content-generator, eval content-translator, eval naming, info, login, logout, server scheduler, server webhooks, type clone, type delete, type diff, type export, type import, type rename, type scaffold, version

<a id="pkg-difflog"></a>
### DiffLog

- Version: `0.0.2`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `requires-configuration`
- Command documentation: `3/3`
- Option documentation: `24/24`
- Argument documentation: `0/0`
- Leaf command examples: `2/3`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: tags

<a id="pkg-inspectra-gen"></a>
### InSpectra.Gen

- Version: `0.0.46`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `8/8`
- Option documentation: `88/88`
- Argument documentation: `4/4`
- Leaf command examples: `0/5`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: render exec html, render exec markdown, render file html, render file markdown, render self

<a id="pkg-karls-gitflow-tool"></a>
### Karls.Gitflow.Tool

- Version: `0.0.13`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `41/41`
- Option documentation: `0/49`
- Argument documentation: `2/31`
- Leaf command examples: `0/35`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: bugfix delete --force, bugfix delete --remote, bugfix finish --fetch, bugfix finish --keep, bugfix finish --push, bugfix finish --quiet, bugfix finish --squash, config save --force, feature delete --force, feature delete --remote, feature finish --fetch, feature finish --keep, feature finish --push, feature finish --quiet, feature finish --squash, hotfix delete --force, hotfix delete --remote, hotfix finish --fetch, hotfix finish --keep, hotfix finish --message, hotfix finish --nobackmerge, hotfix finish --notag, hotfix finish --push, hotfix finish --quiet, hotfix finish --squash, init --bugfix, init --defaults, init --develop, init --feature, init --force, init --hotfix, init --main, init --release, init --save, init --support, init --tag, init --tagmessage, release delete --force, release delete --remote, release finish --fetch, release finish --keep, release finish --message, release finish --nobackmerge, release finish --notag, release finish --push, release finish --quiet, release finish --squash, support delete --force, support delete --remote
- Missing argument descriptions: bugfix delete <name>, bugfix finish <name>, bugfix publish <name>, bugfix start <base>, bugfix start <name>, bugfix track <name>, config set <key>, config set <value>, feature delete <name>, feature finish <name>, feature publish <name>, feature start <base>, feature start <name>, feature track <name>, hotfix delete <name>, hotfix finish <name>, hotfix publish <name>, hotfix start <base>, hotfix start <name>, hotfix track <name>, release delete <name>, release finish <name>, release publish <name>, release start <base>, release start <name>, release track <name>, support delete <name>, support publish <name>, support track <name>
- Missing leaf command examples: bugfix delete, bugfix finish, bugfix list, bugfix publish, bugfix start, bugfix track, config list, config save, config set, feature delete, feature finish, feature list, feature publish, feature start, feature track, hotfix delete, hotfix finish, hotfix list, hotfix publish, hotfix start, hotfix track, init, push, release delete, release finish, release list, release publish, release start, release track, support delete, support list, support publish, support start, support track, version

<a id="pkg-nexudus-cli"></a>
### Nexudus.Cli

- Version: `0.1.5`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `253/253`
- Option documentation: `2195/2348`
- Argument documentation: `58/163`
- Leaf command examples: `0/211`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: accesstokens delete --yes, accesstokens list --page, accesstokens list --query, accesstokens list --size, bookingavailabilityexceptions delete --yes, bookingavailabilityexceptions list --page, bookingavailabilityexceptions list --query, bookingavailabilityexceptions list --size, bookingnotes delete --yes, bookingnotes list --page, bookingnotes list --query, bookingnotes list --size, bookingproducts delete --yes, bookingproducts list --page, bookingproducts list --query, bookingproducts list --size, bookings delete --yes, bookings list --page, bookings list --query, bookings list --size, bookingvisitors delete --yes, bookingvisitors list --page, bookingvisitors list --query, bookingvisitors list --size, businesses list --page, businesses list --query, businesses list --size, cancelledbookings list --page, cancelledbookings list --query, cancelledbookings list --size, checkins delete --yes, checkins list --page, checkins list --query, checkins list --size, countries list --page, countries list --query, countries list --size, coworkerdatafiles delete --yes, coworkerdatafiles list --page, coworkerdatafiles list --query, coworkerdatafiles list --size, coworkerdeliveries delete --yes, coworkerdeliveries list --page, coworkerdeliveries list --query, coworkerdeliveries list --size, coworkergooglecalendars delete --yes, coworkergooglecalendars list --page, coworkergooglecalendars list --query, coworkergooglecalendars list --size, coworkeridentitycheckdocuments delete --yes, coworkeridentitycheckdocuments list --page, coworkeridentitycheckdocuments list --query, coworkeridentitycheckdocuments list --size, coworkeridentitychecks delete --yes, coworkeridentitychecks list --page, coworkeridentitychecks list --query, coworkeridentitychecks list --size, coworkerinventoryassets delete --yes, coworkerinventoryassets list --page, coworkerinventoryassets list --query, coworkerinventoryassets list --size, coworkermessages delete --yes, coworkermessages list --page, coworkermessages list --query, coworkermessages list --size, coworkermsofficecalendars delete --yes, coworkermsofficecalendars list --page, coworkermsofficecalendars list --query, coworkermsofficecalendars list --size, coworkernotes delete --yes, coworkernotes list --page, coworkernotes list --query, coworkernotes list --size, coworkernotifications list --page, coworkernotifications list --query, coworkernotifications list --size, coworkerpriceplanhistories list --page, coworkerpriceplanhistories list --query, coworkerpriceplanhistories list --size, coworkers list --page, coworkers list --query, coworkers list --size, coworkersettings delete --yes, coworkersettings list --page, coworkersettings list --query, coworkersettings list --size, currencies list --page, currencies list --query, currencies list --size, extraservices delete --yes, extraservices list --page, extraservices list --query, extraservices list --size, failedcheckins list --page, failedcheckins list --query, failedcheckins list --size, inventoryassets delete --yes, inventoryassets list --page, inventoryassets list --query, inventoryassets list --size, msofficeadmincalendars delete --yes, msofficeadmincalendars list --page, msofficeadmincalendars list --query, msofficeadmincalendars list --size, productextraservices delete --yes, productextraservices list --page, productextraservices list --query, productextraservices list --size, products delete --yes, products list --page, products list --query, products list --size, producttimepasses delete --yes, producttimepasses list --page, producttimepasses list --query, producttimepasses list --size, resourceaccessruleeligibletimeslots list --page, resourceaccessruleeligibletimeslots list --query, resourceaccessruleeligibletimeslots list --size, resourceaccessrules delete --yes, resourceaccessrules list --page, resourceaccessrules list --query, resourceaccessrules list --size, resourceaccessruletimeslots list --page, resourceaccessruletimeslots list --query, resourceaccessruletimeslots list --size, resources delete --yes, resources list --page, resources list --query, resources list --size, resourcetimeslots delete --yes, resourcetimeslots list --page, resourcetimeslots list --query, resourcetimeslots list --size, resourcetypes delete --yes, resourcetypes list --page, resourcetypes list --query, resourcetypes list --size, teams delete --yes, teams list --page, teams list --query, teams list --size, timepasses delete --yes, timepasses list --page, timepasses list --query, timepasses list --size, visitors delete --yes, visitors list --page, visitors list --query, visitors list --size
- Missing argument descriptions: accesstokens delete <id>, accesstokens get <id>, accesstokens update <id>, bookingavailabilityexceptions delete <id>, bookingavailabilityexceptions get <id>, bookingavailabilityexceptions update <id>, bookingnotes delete <id>, bookingnotes get <id>, bookingnotes update <id>, bookingproducts delete <id>, bookingproducts get <id>, bookingproducts update <id>, bookings delete <id>, bookings get <id>, bookings update <id>, bookingvisitors delete <id>, bookingvisitors get <id>, bookingvisitors update <id>, businesses get <id>, businesses update <id>, cancelledbookings get <id>, checkins delete <id>, checkins get <id>, checkins update <id>, config get <key>, config set <key>, config set <value>, countries get <id>, coworkerdatafiles delete <id>, coworkerdatafiles get <id>, coworkerdatafiles update <id>, coworkerdeliveries delete <id>, coworkerdeliveries get <id>, coworkerdeliveries update <id>, coworkergooglecalendars delete <id>, coworkergooglecalendars get <id>, coworkergooglecalendars update <id>, coworkeridentitycheckdocuments delete <id>, coworkeridentitycheckdocuments get <id>, coworkeridentitycheckdocuments update <id>, coworkeridentitychecks delete <id>, coworkeridentitychecks get <id>, coworkeridentitychecks update <id>, coworkerinventoryassets delete <id>, coworkerinventoryassets get <id>, coworkerinventoryassets update <id>, coworkermessages delete <id>, coworkermessages get <id>, coworkermessages update <id>, coworkermsofficecalendars delete <id>, coworkermsofficecalendars get <id>, coworkermsofficecalendars update <id>, coworkernotes delete <id>, coworkernotes get <id>, coworkernotes update <id>, coworkernotifications get <id>, coworkerpriceplanhistories get <id>, coworkers get <id>, coworkers update <id>, coworkersettings delete <id>, coworkersettings get <id>, coworkersettings update <id>, currencies get <id>, extraservices delete <id>, extraservices get <id>, extraservices update <id>, failedcheckins get <id>, inventoryassets delete <id>, inventoryassets get <id>, inventoryassets update <id>, msofficeadmincalendars delete <id>, msofficeadmincalendars get <id>, msofficeadmincalendars update <id>, productextraservices delete <id>, productextraservices get <id>, productextraservices update <id>, products delete <id>, products get <id>, products update <id>, producttimepasses delete <id>, producttimepasses get <id>, producttimepasses update <id>, resourceaccessrules delete <id>, resourceaccessrules get <id>, resourceaccessrules update <id>, resources delete <id>, resources get <id>, resources update <id>, resourcetimeslots delete <id>, resourcetimeslots get <id>, resourcetimeslots update <id>, resourcetypes delete <id>, resourcetypes get <id>, resourcetypes update <id>, teams delete <id>, teams get <id>, teams update <id>, timepasses delete <id>, timepasses get <id>, timepasses update <id>, visitors delete <id>, visitors get <id>, visitors update <id>
- Missing leaf command examples: accesstokens create, accesstokens delete, accesstokens get, accesstokens list, accesstokens run-command, accesstokens update, bookingavailabilityexceptions create, bookingavailabilityexceptions delete, bookingavailabilityexceptions get, bookingavailabilityexceptions list, bookingavailabilityexceptions run-command, bookingavailabilityexceptions update, bookingnotes create, bookingnotes delete, bookingnotes get, bookingnotes list, bookingnotes run-command, bookingnotes update, bookingproducts create, bookingproducts delete, bookingproducts get, bookingproducts list, bookingproducts run-command, bookingproducts update, bookings create, bookings delete, bookings get, bookings list, bookings run-command, bookings update, bookingvisitors create, bookingvisitors delete, bookingvisitors get, bookingvisitors list, bookingvisitors run-command, bookingvisitors update, businesses get, businesses list, businesses update, cancelledbookings get, cancelledbookings list, checkins create, checkins delete, checkins get, checkins list, checkins run-command, checkins update, config get, config set, countries get, countries list, coworkerdatafiles create, coworkerdatafiles delete, coworkerdatafiles get, coworkerdatafiles list, coworkerdatafiles run-command, coworkerdatafiles update, coworkerdeliveries create, coworkerdeliveries delete, coworkerdeliveries get, coworkerdeliveries list, coworkerdeliveries run-command, coworkerdeliveries update, coworkergooglecalendars create, coworkergooglecalendars delete, coworkergooglecalendars get, coworkergooglecalendars list, coworkergooglecalendars run-command, coworkergooglecalendars update, coworkeridentitycheckdocuments create, coworkeridentitycheckdocuments delete, coworkeridentitycheckdocuments get, coworkeridentitycheckdocuments list, coworkeridentitycheckdocuments run-command, coworkeridentitycheckdocuments update, coworkeridentitychecks create, coworkeridentitychecks delete, coworkeridentitychecks get, coworkeridentitychecks list, coworkeridentitychecks run-command, coworkeridentitychecks update, coworkerinventoryassets create, coworkerinventoryassets delete, coworkerinventoryassets get, coworkerinventoryassets list, coworkerinventoryassets run-command, coworkerinventoryassets update, coworkermessages create, coworkermessages delete, coworkermessages get, coworkermessages list, coworkermessages run-command, coworkermessages update, coworkermsofficecalendars create, coworkermsofficecalendars delete, coworkermsofficecalendars get, coworkermsofficecalendars list, coworkermsofficecalendars run-command, coworkermsofficecalendars update, coworkernotes create, coworkernotes delete, coworkernotes get, coworkernotes list, coworkernotes run-command, coworkernotes update, coworkernotifications get, coworkernotifications list, coworkerpriceplanhistories get, coworkerpriceplanhistories list, coworkers commands, coworkers create, coworkers get, coworkers list, coworkers run-command, coworkers update, coworkersettings create, coworkersettings delete, coworkersettings get, coworkersettings list, coworkersettings run-command, coworkersettings update, currencies get, currencies list, doctor, extraservices create, extraservices delete, extraservices get, extraservices list, extraservices run-command, extraservices update, failedcheckins get, failedcheckins list, inventoryassets create, inventoryassets delete, inventoryassets get, inventoryassets list, inventoryassets run-command, inventoryassets update, login, logout, msofficeadmincalendars create, msofficeadmincalendars delete, msofficeadmincalendars get, msofficeadmincalendars list, msofficeadmincalendars run-command, msofficeadmincalendars update, productextraservices create, productextraservices delete, productextraservices get, productextraservices list, productextraservices run-command, productextraservices update, products commands, products create, products delete, products get, products list, products run-command, products update, producttimepasses create, producttimepasses delete, producttimepasses get, producttimepasses list, producttimepasses update, resourceaccessruleeligibletimeslots list, resourceaccessrules create, resourceaccessrules delete, resourceaccessrules get, resourceaccessrules list, resourceaccessrules run-command, resourceaccessrules update, resourceaccessruletimeslots list, resources create, resources delete, resources get, resources list, resources run-command, resources update, resourcetimeslots create, resourcetimeslots delete, resourcetimeslots get, resourcetimeslots list, resourcetimeslots run-command, resourcetimeslots update, resourcetypes create, resourcetypes delete, resourcetypes get, resourcetypes list, resourcetypes update, teams create, teams delete, teams get, teams list, teams run-command, teams update, timepasses create, timepasses delete, timepasses get, timepasses list, timepasses run-command, timepasses update, visitors create, visitors delete, visitors get, visitors list, visitors run-command, visitors update, whoami

<a id="pkg-phoenixcode-lens"></a>
### PhoenixCode.Lens

- Version: `1.4.2`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `requires-configuration`
- Command documentation: `7/7`
- Option documentation: `18/18`
- Argument documentation: `0/0`
- Leaf command examples: `0/7`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: activate, analyze, config, info, report, run, scan

<a id="pkg-pptmcp-cli"></a>
### PptMcp.CLI

- Version: `1.0.3`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `48/48`
- Option documentation: `350/350`
- Argument documentation: `35/35`
- Leaf command examples: `0/45`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: accessibility, animation, background, batch, chart, comment, customshow, design, diag echo, diag ping, diag validate-params, docproperty, export, file, headerfooter, hyperlink, image, master, media, notes, pagesetup, placeholder, printoptions, proofing, section, service start, service status, service stop, session close, session create, session list, session open, session save, shape, shapealign, slide, slideimport, slideshow, slidetable, smartart, tag, text, transition, vba, window

<a id="pkg-terevintosoftware-staticsitegenerator"></a>
### TerevintoSoftware.StaticSiteGenerator

- Version: `10.0.1`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `requires-configuration`
- Command documentation: `1/1`
- Option documentation: `9/9`
- Argument documentation: `1/1`
- Leaf command examples: `0/1`
- Overall: `FAIL`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: generate

<a id="pkg-atc-kusto"></a>
### atc-kusto

- Version: `3.4.0`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `21/21`
- Option documentation: `75/75`
- Argument documentation: `9/9`
- Leaf command examples: `17/17`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-atc-rest-api-gen"></a>
### atc-rest-api-gen

- Version: `1.0.137`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `14/14`
- Option documentation: `89/89`
- Argument documentation: `0/0`
- Leaf command examples: `10/10`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-blazorlocalization-extractor"></a>
### BlazorLocalization.Extractor

- Version: `10.1.3`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `2/2`
- Option documentation: `12/12`
- Argument documentation: `2/2`
- Leaf command examples: `2/2`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-claudomat"></a>
### claudomat

- Version: `2026.4.1.30`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `3/3`
- Option documentation: `44/44`
- Argument documentation: `3/3`
- Leaf command examples: `3/3`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-cleancli"></a>
### CleanCli

- Version: `1.0.2`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `1/1`
- Option documentation: `0/0`
- Argument documentation: `0/0`
- Leaf command examples: `1/1`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-deadcode"></a>
### DeadCode

- Version: `1.0.2`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `4/4`
- Option documentation: `15/15`
- Argument documentation: `2/2`
- Leaf command examples: `4/4`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-dotnettips-spargine-dev-tool"></a>
### dotNetTips.Spargine.Dev.Tool

- Version: `2026.10.2.16`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `invalid-xml`
- Command documentation: `2/2`
- Option documentation: `12/12`
- Argument documentation: `0/0`
- Leaf command examples: `2/2`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-dotnettokenkiller"></a>
### DotnetTokenKiller

- Version: `0.5.0`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `21/21`
- Option documentation: `36/36`
- Argument documentation: `8/8`
- Leaf command examples: `18/18`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-geomapcli"></a>
### GeoMapCli

- Version: `0.0.8-beta`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `1/1`
- Option documentation: `3/3`
- Argument documentation: `0/0`
- Leaf command examples: `1/1`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None

<a id="pkg-thunderpipe"></a>
### ThunderPipe

- Version: `1.0.2`
- Package status: `ok`
- OpenCLI classification: `json-ready`
- XMLDoc classification: `xml-ready`
- Command documentation: `10/10`
- Option documentation: `24/24`
- Argument documentation: `13/13`
- Leaf command examples: `7/7`
- Overall: `PASS`
- Missing command descriptions: None
- Missing option descriptions: None
- Missing argument descriptions: None
- Missing leaf command examples: None
