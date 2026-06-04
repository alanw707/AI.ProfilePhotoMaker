#!/usr/bin/env node
const { execFileSync } = require('node:child_process');
const { chromium } = require('@playwright/test');

const root = require('node:path').resolve(__dirname, '..', '..');
const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200/';
const dockerCommand = process.env.DOCKER_COMPOSE_COMMAND || 'docker.exe';

function run(command, args, options = {}) {
  return execFileSync(command, args, {
    cwd: root,
    encoding: 'utf8',
    stdio: options.stdio || ['ignore', 'pipe', 'pipe'],
  });
}

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function collectServedBundleText(page) {
  await page.goto(frontendUrl, { waitUntil: 'networkidle' });
  return page.evaluate(async () => {
    const scriptUrls = [...document.scripts].map(script => script.src).filter(Boolean);
    const firstPass = await Promise.all(
      scriptUrls.map(async url => [url, await (await fetch(url)).text()])
    );
    const chunkUrls = new Set(scriptUrls);
    for (const [, body] of firstPass) {
      for (const match of body.matchAll(/\.\/chunk-[A-Z0-9]+\.js/g)) {
        chunkUrls.add(new URL(match[0].slice(2), location.href).href);
      }
    }

    const all = [];
    for (const url of chunkUrls) {
      all.push(await (await fetch(url)).text());
    }
    return all.join('\n');
  });
}

(async () => {
  const ps = run(dockerCommand, ['compose', 'ps', 'frontend']);
  assert(ps.includes('aipm-frontend'), 'frontend container is not listed by docker compose');
  assert(ps.includes('healthy') || ps.includes('Up'), 'frontend container is not running/healthy');

  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage();
    const bundleText = await collectServedBundleText(page);
    const checks = {
      paidConsentGate:
        bundleText.includes('Accept Consent to Continue') &&
        bundleText.includes('Consent is required before generating paid candidates'),
      beforeImageFallback:
        bundleText.includes('Original unavailable') &&
        bundleText.includes('onBeforeImageError') &&
        bundleText.includes('before-fallback'),
      spinnerContrast:
        bundleText.includes('.processing-spinner') &&
        bundleText.includes('width:64px') &&
        bundleText.includes('border-top-color:#5eead4'),
    };

    for (const [name, passed] of Object.entries(checks)) {
      assert(passed, `served frontend bundle missing ${name}`);
    }

    console.log('PASS docker frontend verification');
    console.log(`frontendUrl=${frontendUrl}`);
    console.log(`checks=${Object.keys(checks).join(',')}`);
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(`FAIL docker frontend verification: ${error.message}`);
  process.exit(1);
});
