import { readFileSync, writeFileSync } from 'node:fs';
import { META } from './screens.mjs';

const DIR = 'C:/Users/Administrator/Documents/GitHub/auditx/docs/screenshots';
const manifest = JSON.parse(readFileSync(`${DIR}/manifest.json`, 'utf8')).filter((m) => m.ok);
const esc = (s) => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const SECTION_ORDER = [
  'Getting started', 'Audit universe & planning', 'Templates & response config',
  'Audits & execution', 'Findings, controls & risk', 'Sanctions', 'Reporting',
  'Analytics', 'Audit committee', 'Identity & access', 'Platform administration',
];

const admin = manifest.filter((m) => m.group === 'admin').sort((a, b) => a.n - b.n);
const auditee = manifest.filter((m) => m.group === 'auditee').sort((a, b) => a.n - b.n);

// assign a stable display number to each admin screen (1..N) and auditee (A1..A5)
admin.forEach((m, i) => { m.disp = String(i + 1).padStart(2, '0'); });
auditee.forEach((m, i) => { m.disp = `A${i + 1}`; });

const catOf = (m) => META[m.label]?.cat || 'Other';
const purposeOf = (m) => META[m.label]?.purpose || '';
const cleanTitle = (s) => s.replace(/\s*\(.*?\)\s*$/, '').trim();

// ----- contents -----
const tocSections = SECTION_ORDER.map((sec) => {
  const items = admin.filter((m) => catOf(m) === sec);
  if (!items.length) return '';
  return `<div class="toc-sec"><h3>${esc(sec)}</h3><ul>` +
    items.map((m) => `<li><b>${m.disp}</b> ${esc(cleanTitle(m.label))} <code>${esc(m.path)}</code></li>`).join('') +
    `</ul></div>`;
}).join('');
const tocAuditee = auditee.length ? `<div class="toc-sec toc-aud"><h3>Auditee perspective — signed in as the auditee</h3><ul>` +
  auditee.map((m) => `<li><b>${m.disp}</b> ${esc(cleanTitle(m.label))} <code>${esc(m.path)}</code></li>`).join('') + `</ul></div>` : '';

// ----- figures -----
const figure = (m, personaTag) => `
  <figure class="shot">
    <figcaption>
      <span class="num">${m.disp}</span>
      <span class="cap-label">${esc(cleanTitle(m.label))}</span>
      ${personaTag ? `<span class="persona">Signed in as ${esc(personaTag)}</span>` : ''}
      <span class="cap-path">${esc(m.path)}</span>
    </figcaption>
    <p class="cap-desc">${esc(purposeOf(m))}</p>
    <div class="shot-img"><img src="./${m.file}" alt="${esc(m.label)}"></div>
  </figure>`;

// group admin figures with a section divider page before each section
let adminFigures = '';
for (const sec of SECTION_ORDER) {
  const items = admin.filter((m) => catOf(m) === sec);
  if (!items.length) continue;
  adminFigures += `<section class="divider"><span class="divider-eyebrow">Section</span><h2>${esc(sec)}</h2>
    <p>${items.length} screen${items.length > 1 ? 's' : ''}</p></section>`;
  adminFigures += items.map((m) => figure(m, null)).join('\n');
}

const auditeeSection = auditee.length ? `
  <section class="divider divider-aud">
    <span class="divider-eyebrow">Perspective</span>
    <h2>Signed in as the auditee</h2>
    <p>The same platform seen through an auditee’s account. The left rail is filtered to just the modules their role grants, and each screen is centred on the auditee’s own work — the documents auditors have asked them for, the findings assigned to them, and their self-assessment.</p>
  </section>
  ${auditee.map((m) => figure(m, 'Auditee')).join('\n')}` : '';

const html = `<!doctype html><html lang="en"><head><meta charset="utf-8"><title>AuditX — Annotated Screens</title>
<style>
  :root{ --ink:#1e2330; --muted:#59617a; --faint:#8a91a6; --line:#e4e7f0; --accent:#4f46e5; --accent-soft:#eef0fe; --green:#16a34a; }
  *{box-sizing:border-box}
  @page{ size:A4; margin:11mm 11mm 11mm; }
  html{ -webkit-print-color-adjust:exact; print-color-adjust:exact; }
  body{ font-family:"Segoe UI",-apple-system,Roboto,Arial,sans-serif; color:var(--ink); margin:0; font-size:11px; }
  code{ font-family:"Cascadia Code",Consolas,monospace; font-size:.85em; background:#f3f4f9; border:1px solid var(--line); border-radius:3px; padding:0 4px; color:#334; }

  .cover{ height:273mm; display:flex; flex-direction:column; justify-content:center; }
  .cover .kicker{ font-size:11px; letter-spacing:.16em; text-transform:uppercase; color:var(--accent); font-weight:700; margin:0 0 8px; }
  .cover h1{ font-size:36px; letter-spacing:-.02em; margin:0 0 12px; }
  .cover p{ color:var(--muted); font-size:14px; max-width:155mm; margin:0 0 8px; line-height:1.5; }
  .cover .legend-key{ margin-top:20px; border:1px solid var(--line); border-radius:8px; padding:14px 16px; max-width:150mm; background:#fafbff; }
  .cover .legend-key h4{ margin:0 0 8px; font-size:11px; text-transform:uppercase; letter-spacing:.06em; color:var(--accent); }
  .cover .legend-key ul{ margin:0; padding-left:0; list-style:none; display:grid; grid-template-columns:1fr 1fr; gap:5px 18px; }
  .cover .legend-key li{ font-size:10.5px; color:#33384a; display:flex; gap:7px; align-items:flex-start; }
  .cover .legend-key .pin{ flex:0 0 16px; height:16px; border-radius:50%; background:var(--accent); color:#fff; font-weight:700; font-size:10px; display:inline-flex; align-items:center; justify-content:center; }
  .cover .meta{ margin-top:18px; color:var(--faint); font-size:11px; }
  .page-break{ break-after:page; }

  .toc h2{ font-size:18px; margin:0 0 10px; border-bottom:2px solid var(--accent); padding-bottom:6px; }
  .toc-sec{ break-inside:avoid; margin:0 0 7px; }
  .toc-sec h3{ font-size:11.5px; text-transform:uppercase; letter-spacing:.05em; color:var(--accent); margin:8px 0 3px; }
  .toc-aud h3{ color:var(--green); }
  .toc-sec ul{ margin:0; padding:0; list-style:none; columns:2; column-gap:14px; }
  .toc-sec li{ font-size:10px; margin:2px 0; break-inside:avoid; color:#33384a; }
  .toc-sec li b{ color:var(--accent); font-variant-numeric:tabular-nums; margin-right:3px; }
  .toc-aud li b{ color:var(--green); }
  .toc-sec li code{ color:var(--muted); }

  .divider{ break-before:page; height:70mm; display:flex; flex-direction:column; justify-content:center; border-left:5px solid var(--accent); padding-left:16px; }
  .divider-eyebrow{ font-size:11px; letter-spacing:.14em; text-transform:uppercase; color:var(--accent); font-weight:700; }
  .divider h2{ font-size:26px; margin:6px 0 6px; letter-spacing:-.01em; }
  .divider p{ margin:0; color:var(--muted); font-size:12.5px; max-width:150mm; line-height:1.5; }
  .divider-aud{ border-left-color:var(--green); }
  .divider-aud .divider-eyebrow{ color:var(--green); }

  .shot{ break-before:page; break-inside:avoid; margin:0; height:273mm; display:flex; flex-direction:column; }
  figcaption{ display:flex; align-items:baseline; gap:9px; border-bottom:1px solid var(--line); padding-bottom:6px; margin-bottom:4px; }
  figcaption .num{ background:var(--accent); color:#fff; border-radius:5px; font-weight:700; font-size:11px; padding:2px 8px; font-variant-numeric:tabular-nums; }
  figcaption .cap-label{ font-size:15px; font-weight:700; letter-spacing:-.01em; }
  figcaption .persona{ background:var(--green); color:#fff; font-size:9px; font-weight:700; letter-spacing:.05em; text-transform:uppercase; border-radius:4px; padding:2px 7px; }
  figcaption .cap-path{ margin-left:auto; font-family:"Cascadia Code",Consolas,monospace; font-size:10px; color:var(--faint); }
  .cap-desc{ margin:0 0 8px; font-size:11px; color:#4a5169; line-height:1.45; max-width:180mm; }
  .shot-img{ flex:1; display:flex; align-items:flex-start; justify-content:center; min-height:0; }
  .shot-img img{ max-width:100%; max-height:238mm; object-fit:contain; border:1px solid var(--line); border-radius:6px; box-shadow:0 1px 4px rgba(30,35,48,.08); }
</style></head><body>

<section class="cover page-break">
  <p class="kicker">AuditX Enterprise · On-Premises Edition</p>
  <h1>Application Screens — Annotated</h1>
  <p>Every screen in the internal-audit platform, captured from the running application and marked up in place. On each screen, numbered pins call out the elements, and a footer band names the screen, explains what it is for, and lists what each pin points to.</p>
  <p>The main set is captured as an administrator. A closing section re-captures the auditee’s own screens while signed in <em>as the auditee</em>, so you can see the permission-scoped experience.</p>
  <div class="legend-key">
    <h4>What the pins mean (shell, common to every screen)</h4>
    <ul>
      <li><span class="pin">1</span><span>Module navigation — the left rail, filtered to your permissions.</span></li>
      <li><span class="pin">2</span><span>Global search, plus theme, text-size and language.</span></li>
      <li><span class="pin">3</span><span>Signed-in account and sign out.</span></li>
      <li><span class="pin">4</span><span>Screen title and its primary actions.</span></li>
      <li><span class="pin">5</span><span>The screen’s main workspace (described per screen).</span></li>
    </ul>
  </div>
  <p class="meta">${admin.length} administrator screens · ${auditee.length} auditee screens · captured ${new Date().toISOString().slice(0, 10)} · light theme, 1440&nbsp;px width</p>
</section>

<section class="toc page-break">
  <h2>Contents</h2>
  ${tocSections}
  ${tocAuditee}
</section>

${adminFigures}
${auditeeSection}

</body></html>`;

writeFileSync(`${DIR}/gallery.html`, html);
console.log(`gallery.html written: ${admin.length} admin + ${auditee.length} auditee screens`);
