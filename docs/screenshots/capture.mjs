import { chromium } from 'playwright-core';
import { mkdirSync, writeFileSync, readFileSync } from 'node:fs';
import { META } from './screens.mjs';

const AUDITEE_ONLY = !!process.env.AUDITEE_ONLY;

const BASE = 'http://localhost:4288';
const API = 'http://localhost:8085/api/v1';
const OUT = 'C:/Users/Administrator/Documents/GitHub/auditx/docs/screenshots';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const VIEW = { width: 1440, height: 1600 };
mkdirSync(OUT, { recursive: true });

const SHELL_CALLOUTS = [
  { sel: '.nav__scroll', note: 'Module navigation — switch between audit areas (filtered to your permissions).' },
  { sel: '.layout__toolbar', note: 'Global search, plus theme, text-size and language controls.' },
  { sel: '.nav__profile', note: 'Signed-in account and sign out.' },
  { sel: 'app-page-header', note: 'Screen title and its primary actions.' },
];
const CONTENT_SEL = '.layout__main table, .layout__main app-data-table, .layout__main .ax-card, .layout__main mat-card, .layout__main [class*="card"], .layout__main [class*="table"], .layout__main section, .layout__main';

// ---- The annotation overlay, injected into the page then captured ----
function overlay(payload) {
  document.getElementById('ax-annot')?.remove();
  const { title, purpose, persona, callouts } = payload;
  const BAND_H = 128;
  const vw = window.innerWidth;
  const vh = window.innerHeight;

  const rectOf = (c) => {
    if (c.content) {
      // first real data block BELOW the page header (avoids overlapping the header ring)
      const ph = document.querySelector('app-page-header');
      const minTop = ph ? ph.getBoundingClientRect().bottom - 4 : 0;
      const cands = [...document.querySelectorAll('.layout__main .ax-card, .layout__main mat-card, .layout__main table, .layout__main app-data-table, .layout__main [class*="card"], .layout__main section')];
      for (const el of cands) {
        const r = el.getBoundingClientRect();
        if (r.top >= minTop && r.width > 40 && r.height > 24) return r;
      }
      const m = document.querySelector('.layout__main');
      return m ? m.getBoundingClientRect() : null;
    }
    const el = document.querySelector(c.sel);
    return el ? el.getBoundingClientRect() : null;
  };

  const found = [];
  for (const c of callouts) {
    const r = rectOf(c);
    if (!r || r.width < 8 || r.height < 8) continue;
    if (r.top > vh - 40 || r.bottom < 40 || r.left > vw - 20) continue;
    found.push({ note: c.note, r });
  }

  const root = document.createElement('div');
  root.id = 'ax-annot';
  root.style.cssText = 'position:fixed;inset:0;z-index:2147483647;pointer-events:none;font-family:"Segoe UI",Roboto,Arial,sans-serif;';

  const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));
  found.forEach((f, i) => {
    const r = f.r;
    // clamp the ring to the visible area above the legend band
    const top = Math.max(2, r.top);
    const bottom = Math.min(vh - BAND_H - 4, r.bottom);
    const left = Math.max(2, r.left);
    const right = Math.min(vw - 2, r.right);
    const ring = document.createElement('div');
    ring.style.cssText = `position:fixed;left:${left - 2}px;top:${top - 2}px;width:${Math.max(10, right - left) + 4}px;height:${Math.max(10, bottom - top) + 4}px;border:2.5px solid #4f46e5;border-radius:8px;box-shadow:0 0 0 3px rgba(79,70,229,.16);`;
    // badge sits just inside the ring's top-left corner, always fully on-screen
    const bx = clamp(left - 13, 6, vw - 32);
    const by = clamp(top - 13, 6, vh - BAND_H - 32);
    const badge = document.createElement('div');
    badge.textContent = i + 1;
    badge.style.cssText = `position:fixed;left:${bx}px;top:${by}px;width:26px;height:26px;border-radius:50%;background:#4f46e5;color:#fff;font-weight:700;font-size:14px;display:flex;align-items:center;justify-content:center;box-shadow:0 2px 6px rgba(0,0,0,.35);border:2px solid #fff;`;
    root.appendChild(ring);
    root.appendChild(badge);
  });

  // ---- bottom legend band ----
  const band = document.createElement('div');
  band.style.cssText = `position:fixed;left:0;right:0;bottom:0;height:${BAND_H}px;background:rgba(255,255,255,.97);border-top:3px solid #4f46e5;box-shadow:0 -6px 20px rgba(30,35,48,.14);display:flex;gap:18px;padding:12px 20px;box-sizing:border-box;`;
  const left = document.createElement('div');
  left.style.cssText = 'flex:0 0 40%;max-width:40%;border-right:1px solid #e4e7f0;padding-right:16px;';
  const personaChip = persona ? `<span style="display:inline-block;background:#16a34a;color:#fff;font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;border-radius:4px;padding:2px 7px;margin-right:8px;vertical-align:middle;">Signed in as ${persona}</span>` : '';
  left.innerHTML = `<div style="font-size:16px;font-weight:800;color:#1e2330;margin-bottom:5px;letter-spacing:-.01em;">${personaChip}${title}</div>
    <div style="font-size:11.5px;line-height:1.45;color:#4a5169;">${purpose}</div>`;
  const right = document.createElement('div');
  right.style.cssText = 'flex:1 1 60%;display:grid;grid-template-columns:1fr 1fr;gap:4px 20px;align-content:start;overflow:hidden;';
  right.innerHTML = found.map((f, i) => `<div style="display:flex;gap:7px;align-items:flex-start;font-size:10.5px;line-height:1.35;color:#33384a;">
      <span style="flex:0 0 17px;height:17px;border-radius:50%;background:#4f46e5;color:#fff;font-weight:700;font-size:11px;display:inline-flex;align-items:center;justify-content:center;margin-top:1px;">${i + 1}</span>
      <span>${f.note}</span></div>`).join('');
  band.appendChild(left);
  band.appendChild(right);
  root.appendChild(band);
  document.body.appendChild(root);
  return found.length;
}

const LOGIN_CALLOUTS = [
  { sel: 'input[autocomplete="username"], input[formcontrolname="username"]', note: 'Bank username.' },
  { sel: 'input[autocomplete="current-password"], input[type="password"]', note: 'Password — sessions are scoped to your role’s permissions.' },
  { sel: 'button[type="submit"]', note: 'Sign in.' },
];

async function settle(page) {
  await page.waitForTimeout(300);
  await page.keyboard.press('Escape').catch(() => {});
  await page.waitForTimeout(1500);
}

async function shot(page, entry) {
  const isLogin = entry.path === '/login';
  await page.goto(`${BASE}${entry.path}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
  await page.waitForSelector(isLogin ? 'button[type="submit"]' : '.layout__main', { timeout: 20000 }).catch(() => {});
  await settle(page);
  const meta = META[entry.label] || { purpose: '', spot: 'Main content.' };
  const payload = {
    title: entry.label.replace(/\s*\(.*?\)\s*$/, '').trim(),
    purpose: meta.purpose,
    persona: entry.persona || null,
    callouts: isLogin ? LOGIN_CALLOUTS : [...SHELL_CALLOUTS, { content: true, note: meta.spot }],
  };
  const nFound = await page.evaluate(overlay, payload);
  await page.waitForTimeout(150);
  await page.screenshot({ path: `${OUT}/${entry.file}`, fullPage: false });
  return nFound;
}

// ============================ run ============================
const browser = await chromium.launch({ executablePath: CHROME, headless: true });

async function login(context, user) {
  const page = await context.newPage();
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' });
  await page.fill('input[autocomplete="username"]', user);
  await page.fill('input[autocomplete="current-password"]', 'Passw0rd!');
  await Promise.all([
    page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 20000 }).catch(() => {}),
    page.click('button[type="submit"]'),
  ]);
  await page.waitForTimeout(1500);
  return page;
}

async function first(ctx, path, pick = (d) => (d?.items ? d.items[0] : Array.isArray(d) ? d[0] : d)) {
  try {
    const r = await ctx.request.get(`${API}${path}`);
    if (!r.ok()) return null;
    const body = await r.json();
    return pick(body.data) ?? null;
  } catch { return null; }
}

let manifest = [];
const slugify = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 50);

// ---------- ADMIN PASS (all screens) ----------
if (AUDITEE_ONLY) {
  // keep the already-captured admin screens; only the auditee pass re-runs
  manifest = JSON.parse(readFileSync(`${OUT}/manifest.json`, 'utf8')).filter((m) => m.group === 'admin');
  console.log(`AUDITEE_ONLY: preserving ${manifest.length} admin screens from manifest`);
}
const adminCtx = AUDITEE_ONLY ? null : await browser.newContext({ viewport: VIEW, deviceScaleFactor: 1.5 });
if (!AUDITEE_ONLY) {
const adminPage = await login(adminCtx, 'admin');
console.log('admin logged in:', adminPage.url());

const auditId = (await first(adminCtx, '/audits?pageSize=5'))?.id;
const exceptionId = (await first(adminCtx, '/exceptions?pageSize=5'))?.id;
const planId = (await first(adminCtx, '/annual-plans'))?.id;
const templateId = (await first(adminCtx, '/templates?status=all&pageSize=5'))?.id;
const userId = (await first(adminCtx, '/users?limit=5', (d) => d?.items?.[0]))?.id;
const roleId = (await first(adminCtx, '/roles'))?.id;
const sanctionsId = (await first(adminCtx, '/sanctions/cases?pageSize=5'))?.id;
const dash = await first(adminCtx, '/dashboards');
const dashKey = dash?.slug ?? dash?.id;
const reportId = (await first(adminCtx, `/audits/${auditId}/reports`))?.id;
const clusterId = (await first(adminCtx, '/analytics/recurrence-clusters?pageSize=5'))?.id;
console.log({ auditId, exceptionId, planId, templateId, userId, roleId, sanctionsId, dashKey, reportId, clusterId });

const S = [];
const add = (label, path, cond = true) => { if (cond && path) S.push({ label, path }); };
add('Login', '/login');
add('Dashboard', '/dashboard');
add('My Evidence Requests (auditee upload worklist)', '/my/evidence-requests');
add('Audit Universe — Entities', '/audit-universe');
add('Risk Scoring Dimensions', '/audit-universe/risk-dimensions');
add('Organisational Units', '/audit-universe/org-units');
add('Annual Plans', '/planning');
add('Annual Plan — detail', planId && `/planning/${planId}`);
add('Coverage Analytics', '/coverage');
add('Checklist Templates', '/admin/templates');
add('Template Editor', templateId && `/admin/templates/${templateId}`);
add('Rating Scales', '/admin/templates/rating-scales');
add('Response Labels (custom option sets)', '/admin/response-labels');
add('Audits', '/audits');
add('Audit — detail', auditId && `/audits/${auditId}`);
add('Audit Execution / Fieldwork', auditId && `/audits/${auditId}/execute`);
add('Audit Reports', auditId && `/audits/${auditId}/reports`);
add('Engagement Lifecycle — portfolio board', '/engagement-lifecycle');
add('Engagement Journey / next-best-action', auditId && `/engagement-lifecycle/${auditId}`);
add('Findings / Exceptions register', '/exceptions');
add('Finding — detail', exceptionId && `/exceptions/${exceptionId}`);
add('Root-cause Gaps', '/root-cause-gaps');
add('Control Register', '/controls');
add('Regulations Register', '/compliance');
add('Enterprise Risk Register', '/risks');
add('Exception-raising Rules', '/admin/exception-raising-rules');
add('Sanctions Cases', '/admin/sanctions');
add('Sanctions Case — detail', sanctionsId && `/admin/sanctions/${sanctionsId}`);
add('Disciplinary Committee Queue', '/admin/sanctions/dc-queue');
add('Sanctions Grid', '/admin/sanctions/grid');
add('Reports', '/reports');
add('Report — detail', reportId && `/reports/${reportId}`);
add('Report Templates', '/admin/report-templates');
add('Report Schedules', '/admin/report-schedules');
add('Analytics — KPIs', '/analytics');
add('Custom Dashboard', dashKey && `/analytics/dashboards/${dashKey}`);
add('Performance Scorecards', '/analytics/scorecards');
add('Auditor Throughput', '/analytics/auditor-throughput');
add('Org-unit Scorecards', '/analytics/org-units');
add('Budget vs Actual', '/analytics/time-budget');
add('Auditor Workload', '/analytics/auditor-workload');
add('KPI Trends', '/analytics/trends');
add('Risk Heatmap', '/analytics/risk-heatmap');
add('Controls Compliance', '/analytics/controls-compliance');
add('Recurrence Clusters', '/analytics/recurrence-clusters');
add('Recurrence Cluster — detail', clusterId && `/analytics/recurrence-clusters/${clusterId}`);
add('Audit Committee — workspace', '/ac');
add('Users', '/admin/users');
add('User — detail', userId && `/admin/users/${userId}`);
add('Roles', '/admin/roles');
add('Role Editor', roleId && `/admin/roles/${roleId}`);
add('Maker-checker Queue', '/admin/maker-checker');
add('Bank Settings / Administration', '/admin/administration');
add('Audit Trail', '/admin/audit-trail');
add('Configuration Store', '/admin/configuration');
add('Configuration — exception defaults', '/admin/configuration/exception_defaults');
add('Reference Data', '/admin/reference-data');
add('Integrations', '/admin/integrations');
add('Webhooks', '/admin/webhooks');
add('Notifications (rules / templates / dispatches)', '/admin/notifications');
add('Evidence Integrity (flagged files)', '/admin/evidence-integrity');
add('My Notification Preferences', '/account/notification-preferences');

let n = 0;
for (const s of S) {
  n++;
  const file = `${String(n).padStart(2, '0')}-${slugify(s.label)}.png`;
  const entry = { ...s, file, n };
  try {
    const found = await shot(adminPage, entry);
    manifest.push({ n, group: 'admin', persona: null, label: s.label, path: s.path, file, ok: true, callouts: found });
    console.log('OK  ', file, `(${found} callouts)`);
  } catch (e) {
    manifest.push({ n, group: 'admin', label: s.label, path: s.path, file: null, ok: false, error: String(e).slice(0, 120) });
    console.log('FAIL', file, String(e).slice(0, 90));
  }
}

// ---------- SEED an evidence request to the auditee, so the worklist isn't empty ----------
const AUDITEE_ID = '019ff490-0f21-7262-aa06-8f99984ba107';
const SEED_MARK = 'Q3 access-control review';
if (auditId) {
  try {
    const existing = await first(adminCtx, `/audits/${auditId}/evidence-requests`, (d) => (Array.isArray(d) ? d : d?.items || []));
    const already = Array.isArray(existing) && existing.some((x) => (x.title || '').startsWith(SEED_MARK));
    if (already) {
      console.log('seed skipped: auditee requests already present');
    } else {
      const r1 = await adminCtx.request.post(`${API}/audits/${auditId}/evidence-requests`, {
        data: {
          RequestedFromUserId: AUDITEE_ID,
          Title: 'Q3 access-control review — user access listing',
          DocumentType: 'Access listing (CSV/PDF)',
          DueDate: new Date(Date.now() + 6 * 864e5).toISOString().slice(0, 10),
          Purpose: 'ReviewDocument',
          Notes: 'Please export the full active-user access listing for the in-scope application and upload it here.',
        },
      });
      console.log('seed #1 (ReviewDocument):', r1.status());
      const r2 = await adminCtx.request.post(`${API}/audits/${auditId}/evidence-requests`, {
        data: {
          RequestedFromUserId: AUDITEE_ID,
          ExceptionId: exceptionId || undefined,
          Title: 'Q3 access-control review — remediation evidence for change approvals',
          DocumentType: 'Approval records',
          DueDate: new Date(Date.now() + 3 * 864e5).toISOString().slice(0, 10),
          Purpose: exceptionId ? 'FindingEvidence' : 'ReviewDocument',
          Notes: 'Attach the approval record for each of the 10 sampled changes.',
        },
      });
      console.log('seed #2 (FindingEvidence):', r2.status());
    }
  } catch (e) { console.log('seed failed:', String(e).slice(0, 160)); }
}
await adminPage.close();
} // end !AUDITEE_ONLY

// ---------- AUDITEE PASS (auditee-specific screens, signed in as auditee) ----------
const audCtx = await browser.newContext({ viewport: VIEW, deviceScaleFactor: 1.5 });
const audPage = await login(audCtx, 'auditee');
console.log('auditee logged in:', audPage.url());

const audExceptionId = (await first(audCtx, '/exceptions?pageSize=5'))?.id;
const AUD = [];
const addA = (label, path, cond = true) => { if (cond && path) AUD.push({ label, path }); };
addA('Dashboard', '/dashboard');
addA('My Evidence Requests (auditee upload worklist)', '/my/evidence-requests');
addA('Findings / Exceptions register', '/exceptions');
addA('Finding — detail', audExceptionId && `/exceptions/${audExceptionId}`);
addA('Engagement Lifecycle — portfolio board', '/engagement-lifecycle');

let a = 0;
for (const s of AUD) {
  a++;
  const file = `aud-${a}-${slugify(s.label)}.png`;
  const entry = { ...s, file, persona: 'Auditee' };
  try {
    const found = await shot(audPage, entry);
    manifest.push({ n: 1000 + a, group: 'auditee', persona: 'Auditee', label: s.label, path: s.path, file, ok: true, callouts: found });
    console.log('OK  ', file, `(${found} callouts)`);
  } catch (e) {
    manifest.push({ n: 1000 + a, group: 'auditee', persona: 'Auditee', label: s.label, path: s.path, file: null, ok: false, error: String(e).slice(0, 120) });
    console.log('FAIL', file, String(e).slice(0, 90));
  }
}
await audPage.close();

writeFileSync(`${OUT}/manifest.json`, JSON.stringify(manifest, null, 2));
console.log(`\nDONE: ${manifest.filter((m) => m.ok).length}/${manifest.length} screens captured`);
await browser.close();
