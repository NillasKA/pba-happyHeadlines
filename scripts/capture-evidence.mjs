import { chromium } from 'playwright';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { platform, arch, release } from 'node:os';

const output = 'artifacts/evidence';
const baseline = '3854063ce56689b4b7355a53323a16e3bd7b5d00';
await mkdir(output, { recursive: true });
const revision = execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
await writeFile(join(output, 'manifest.json'), JSON.stringify({ revision, capturedAtUtc: new Date().toISOString(), platform: platform(), architecture: arch(), kernelRelease: release(), nodeVersion: process.version, baselineRevision: baseline }, null, 2) + '\n');
const browser = await chromium.launch({ headless: true });
const errors = [];
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function attempt(name, action) {
  try { await action(); console.log(`Captured ${name}`); }
  catch (error) { errors.push(`${name}: ${error.message}`); console.error(errors.at(-1)); }
}
async function json(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${url}: HTTP ${response.status}`);
  return response.json();
}
async function waitFor(check, label) {
  for (let i = 0; i < 30; i++) {
    try { const value = await check(); if (value) return value; } catch { /* Retry while services start. */ }
    await delay(2000);
  }
  throw new Error(`${label} was not observed`);
}
const page = await browser.newPage({ viewport: { width: 1600, height: 1050 }, deviceScaleFactor: 1 });

await attempt('metrics', async () => {
  const query = 'sum by (service) (happy_headlines_http_requests_total)';
  await waitFor(async () => (await json('http://localhost:9090/api/v1/query?query=' + encodeURIComponent(query))).data.result.some(series => /ArticleService/i.test(series.metric.service ?? '')), 'ArticleService request series');
  try {
    await page.goto('http://localhost:9090/graph?g0.expr=' + encodeURIComponent(query) + '&g0.tab=1', { waitUntil: 'domcontentloaded' });
    await page.getByText(/ArticleService/i).first().waitFor({ timeout: 45000 });
    await page.screenshot({ path: join(output, 'metrics-prometheus.png'), fullPage: false });
  } catch (error) {
    await page.screenshot({ path: join(output, 'metrics-debug.png'), fullPage: false }).catch(() => {});
    const body = await page.locator('body').innerText({ timeout: 3000 }).catch(() => 'Body unavailable');
    await writeFile(join(output, 'metrics-debug.txt'), `URL: ${page.url()}\n${body}\n`);
    throw error;
  }
});
await attempt('structured logs', async () => {
  await waitFor(async () => { const events = await json('http://localhost:8083/api/events?count=100'); const items = Array.isArray(events) ? events : (events.Events ?? events.events ?? events.Items ?? []); return items.some(event => /PublisherService|NewsletterService|ArticleService/.test(JSON.stringify(event))); }, 'Seq events');
  await page.goto('http://localhost:8083/', { waitUntil: 'domcontentloaded' });
  const event = page.getByText(/Persisted queued article/).first();
  await event.waitFor({ timeout: 30000 });
  await event.click();
  await page.getByText('ArticleId', { exact: false }).first().waitFor({ timeout: 10000 });
  await page.getByText('ArticleService', { exact: false }).first().waitFor({ timeout: 10000 });
  await page.screenshot({ path: join(output, 'logging-seq.png'), fullPage: true });
});
await attempt('distributed trace', async () => {
  const trace = await waitFor(async () => {
    const traces = await json('http://localhost:9411/api/v2/traces?limit=100');
    return traces.find(spans => new Set(spans.map(span => span.localEndpoint?.serviceName).filter(Boolean)).size >= 2);
  }, 'multi-service trace');
  await page.goto(`http://localhost:9411/zipkin/traces/${trace[0].traceId}`, { waitUntil: 'domcontentloaded' });
  await page.getByText(/publish|newsletter|article/i).first().waitFor({ timeout: 30000 });
  await page.screenshot({ path: join(output, 'tracing-zipkin.png'), fullPage: true });
});
for (const [view, file, required] of [['containers', 'c4-containers.png', 'ArticleService'], ['article-components', 'c4-article-components.png', 'ArticleRepository']]) {
  await attempt(`C4 ${view}`, async () => {
    await page.goto(`http://localhost:8081/workspace/diagrams#${view}`, { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => window.structurizr?.scripting?.isDiagramRendered() === true, null, { timeout: 30000 });
    await page.getByText(required, { exact: false }).first().waitFor({ timeout: 30000 });
    const png = await page.evaluate(() => new Promise((resolve, reject) => {
      window.structurizr.scripting.exportCurrentDiagramToPNG({ includeMetadata: true, crop: true }, data => {
        if (typeof data !== 'string' || !data.startsWith('data:image/png;base64,')) reject(new Error('PNG export returned no image'));
        else resolve(data);
      });
      setTimeout(() => reject(new Error('PNG export timed out')), 30000);
    }));
    await writeFile(join(output, file), Buffer.from(png.split(',')[1], 'base64'));
  });
}
const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const diffs = [
  ['gaf-pagination.png', 'Defer Fetching/Lazy Fetch', 'src/ArticleService/Controllers/ArticlesController.cs', 'Unbounded:', 'Limit 1:'],
  ['gaf-compression.png', 'Reduce Network Package Size', 'src/ArticleService/Program.cs', 'Gzip response:', 'Content-Encoding:'],
  ['gaf-image.png', 'Reduce Bundle Size', 'src/ArticleService/Dockerfile', 'Runtime image sizes', 'mcr.microsoft.com/dotnet/aspnet']
];
for (const [file, title, source, marker1, marker2] of diffs) {
  await attempt(title, async () => {
    const measurement = await readFile('evidence/gaf-measurements.log', 'utf8');
    if (!measurement.includes(marker1) || !measurement.includes(marker2)) throw new Error('Measurement output is incomplete');
    const diff = execFileSync('git', ['diff', '--no-ext-diff', '--unified=3', baseline, 'HEAD', '--', source], { encoding: 'utf8' });
    if (!diff.trim()) throw new Error(`No source change against baseline for ${source}`);
    const content = `<!doctype html><meta charset="utf-8"><style>body{font:18px sans-serif;margin:35px}pre{font:15px monospace;white-space:pre-wrap;border:1px solid #aaa;padding:16px}h1{font-size:27px}</style><h1>${escape(title)}</h1><p>Source change against baseline ${baseline}: ${escape(source)}</p><pre>${escape(diff)}</pre><h2>Observed measurements</h2><pre>${escape(measurement)}</pre><p>Measurements are local observations; energy and carbon impact were not measured.</p>`;
    await page.setContent(content);
    await page.screenshot({ path: join(output, file), fullPage: true });
  });
}
await browser.close();
if (errors.length) { console.error(`Evidence capture incomplete:\n${errors.join('\n')}`); process.exitCode = 1; }
