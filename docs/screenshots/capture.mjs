import { chromium } from 'playwright-core';
import { mkdirSync, writeFileSync } from 'node:fs';

const BASE = 'http://localhost:4288';
const API = 'http://localhost:8085/api/v1';
const OUT = 'C:/Users/Administrator/Documents/GitHub/auditx/docs/screenshots';
const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
mkdirSync(OUT, { recursive: true });

const browser = await chromium.launch({ executablePath: CHROME, headless: true });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1.5 });
const page = await context.newPage();

// ---- Log in as admin ----
await page.goto(`${BASE}/login`, { waitUntil: 'networkidle' });
await page.fill('input[autocomplete="username"]', 'admin');
await page.fill('input[autocomplete="current-password"]', 'Passw0rd!');
await Promise.all([
  page.waitForURL((u) => !u.pathname.endsWith('/login'), { timeout: 20000 }).catch(() => {}),
  page.click('button[type="submit"]'),
]);
await page.waitForTimeout(2000);
console.log('logged in, url =', page.url());

// ---- Resolve IDs for parameterized routes (authenticated request shares the session) ----
async function first(path, pick = (d) => (d.items ? d.items[0] : Array.isArray(d) ? d[0] : d)) {
  try {
    const r = await context.request.get(`${API}${path}`);
    if (!r.ok()) return null;
    const body = await r.json();
    const item = pick(body.data);
    return item ?? null;
  } catch { return null; }
}
const auditId = (await first('/audits?pageSize=5'))?.id;
const exceptionId = (await first('/exceptions?pageSize=5'))?.id;
const planId = (await first('/annual-plans'))?.id;
const templateId = (await first('/templates?status=all&pageSize=5'))?.id;
const userId = (await first('/users?limit=5', (d) => d.items?.[0]))?.id;
const roleId = (await first('/roles'))?.id;
const sanctionsId = (await first('/sanctions/cases?pageSize=5'))?.id;
const dash = await first('/dashboards');
const dashKey = dash?.slug ?? dash?.id;
const reportId = (await first(`/audits/${auditId}/reports`))?.id;
const clusterId = (await first('/analytics/recurrence-clusters?pageSize=5'))?.id;
console.log({ auditId, exceptionId, planId, templateId, userId, roleId, sanctionsId, dashKey, reportId, clusterId });

// ---- The full ordered screen list ----
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

// ---- Capture each ----
const manifest = [];
let n = 0;
for (const s of S) {
  n++;
  const slug = String(n).padStart(2, '0') + '-' + s.label.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 50);
  const file = `${slug}.png`;
  try {
    await page.goto(`${BASE}${s.path}`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.keyboard.press('Escape').catch(() => {}); // dismiss any guide/overlay
    await page.waitForTimeout(1600); // let charts/data settle
    await page.screenshot({ path: `${OUT}/${file}`, fullPage: true });
    manifest.push({ n, label: s.label, path: s.path, file, ok: true });
    console.log('OK  ', slug, s.path);
  } catch (e) {
    manifest.push({ n, label: s.label, path: s.path, file: null, ok: false, error: String(e).slice(0, 120) });
    console.log('FAIL', slug, s.path, String(e).slice(0, 80));
  }
}

writeFileSync(`${OUT}/manifest.json`, JSON.stringify(manifest, null, 2));
console.log(`\n${manifest.filter((m) => m.ok).length}/${manifest.length} screens captured`);
await browser.close();
