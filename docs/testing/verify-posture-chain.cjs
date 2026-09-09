// Real LOCAL API + SQL + Azurite integration. No API/browser responses are mocked.
// Run with an authenticated local fixture account; --seed-local grants only missing test allowances.
const fs = require('node:fs');
const path = require('node:path');
const cp = require('node:child_process');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../..');
const { request } = require(path.join(root, 'AI.ProfilePhotoMaker.UI/node_modules/playwright'));
const base = process.env.AIPM_VERIFY_DIR || '/tmp/aipm-compose-verify';
const output = path.join(base, 'posture-chain-host');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const probe = path.join(__dirname, 'posture-chain-probe/bin/Debug/net10.0/PostureChainProbe.dll');
const dockerRead = file => cp.execFileSync('docker', ['exec', 'aipmverify-provider-fixture-1', 'node', '-p',
  `require('fs').readFileSync('/evidence/posture-chain/${file}', 'utf8')`], { encoding: 'utf8' });

(async () => {
  fs.mkdirSync(output, { recursive: true });
  assert.equal(cp.execFileSync('docker', ['exec', 'aipmverify-api-1', 'printenv', 'OpenAI__BaseUrl'],
    { encoding: 'utf8' }).trim(), 'http://provider-fixture:5055/v1/', 'Refusing non-fixture provider');
  const env = Object.fromEntries(fs.readFileSync(path.join(base, 'base.env'), 'utf8').split('\n')
    .filter(line => line.includes('=')).map(line => { const i = line.indexOf('='); return [line.slice(0, i), line.slice(i + 1)]; }));
  const email = JSON.parse(fs.readFileSync(path.join(base, 'account.json'), 'utf8')).email.replaceAll("'", "''");
  const sql = query => {
    const result = cp.spawnSync('docker', ['exec', '-e', 'SQLCMDPASSWORD=' + env.MSSQL_SA_PASSWORD,
      'aipmverify-sql-server-1', '/opt/mssql-tools18/bin/sqlcmd', '-S', 'localhost', '-U', 'sa', '-C',
      '-d', 'AIProfileMakerVerify', '-b', '-y', '0', '-w', '65535', '-Q', 'SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; ' + query],
    { encoding: 'utf8', timeout: 30000 });
    if (result.status !== 0) {
      fs.writeFileSync(path.join(output, 'sql-error.private.txt'), (result.stderr || '') + (result.stdout || ''), { mode: 0o600 });
      throw Error('Local fixture SQL failed; credentials/details suppressed');
    }
    return result.stdout.trim();
  };
  const profileFilter = `i.UserProfileId IN (SELECT p.Id FROM UserProfiles p JOIN AspNetUsers u ON u.Id=p.UserId WHERE u.Email=N'${email}')`;
  const rows = query => JSON.parse(sql(query + ' FOR JSON PATH;'));
  const imageRow = id => rows(`SELECT i.Id,i.UserProfileId,i.OriginalImageUrl,i.ProcessedImageUrl,i.ReplacesProcessedImageId,i.PromptVersion FROM ProcessedImages i WHERE i.Id=${Number(id)} AND ${profileFilter}`)[0];
  const original = rows(`SELECT TOP(1) i.OriginalImageUrl FROM ProcessedImages i WHERE ${profileFilter} AND i.GenerationMode='instant_headshot' ORDER BY i.Id`)[0];
  assert.ok(original?.OriginalImageUrl, 'Local account needs an existing uploaded photo');
  const api = await request.newContext({ storageState: path.join(base, 'auth-state.json') });
  try {
    const get = async endpoint => {
      const response = await api.get('http://localhost:5032/api/' + endpoint);
      assert.ok(response.ok(), 'Local API read failed; refresh verification login');
      const body = await response.json(); assert.ok(body.success); return body.data;
    };
    const post = async data => {
      const response = await api.post('http://localhost:5032/api/headshots/generate', { data, timeout: 180000 });
      const body = await response.json();
      assert.ok(response.ok() && body.success, 'Local generation rejected: ' + response.status() + ' ' + (body.error?.code || ''));
      return body.data;
    };
    const image = async storagePath => {
      const response = await api.get('http://localhost:5032/profile-images/' + storagePath);
      assert.ok(response.ok(), 'Saved blob unavailable'); return response.body();
    };
    let entitlements = await get('profilephotoworkflow/entitlements');
    let entitlement = entitlements.find(e => e.packageCode === 'pro_package');
    assert.ok(entitlement, 'Local fixture needs a Pro entitlement');
    if (process.argv.includes('--seed-local')) {
      assert.ok(Number.isInteger(entitlement.id));
      sql(`UPDATE e SET RemainingCandidates=CASE WHEN RemainingCandidates<1 THEN 1 ELSE RemainingCandidates END,
        RemainingRefinements=CASE WHEN RemainingRefinements<1 THEN 1 ELSE RemainingRefinements END,
        RemainingPackageUses=CASE WHEN RemainingPackageUses<1 THEN 1 ELSE RemainingPackageUses END
        FROM UserPackageEntitlements e JOIN AspNetUsers u ON u.Id=e.UserId WHERE e.Id=${entitlement.id} AND u.Email=N'${email}' AND e.Status=0;`);
      entitlements = await get('profilephotoworkflow/entitlements');
      entitlement = entitlements.find(e => e.id === entitlement.id);
    }
    assert.ok(entitlement.remainingCandidates > 0 && entitlement.remainingRefinements > 0, 'Seed missing LOCAL test allowances explicitly');
    const count = () => JSON.parse(dockerRead('count.json')).requests;
    const startCount = count();
    // Make the selected proof through the real generation endpoint/storage writer.
    const selected = await post({ imageStoragePath: original.OriginalImageUrl, style: 'linkedin',
      packageCode: 'pro_package', numOutputs: 1, isRegeneration: false, clientRequestId: crypto.randomUUID() });
    assert.equal(count(), startCount + 1);
    const selectedRow = imageRow(selected.processedImageId);
    assert.equal(selectedRow.OriginalImageUrl, original.OriginalImageUrl);
    const selectedBytes = await image(selected.storagePath);
    assert.equal(hash(selectedBytes), hash(fs.readFileSync(path.join(output, 'source.png'))));
    assert.notEqual(hash(selectedBytes), hash(await image(original.OriginalImageUrl)), 'Selected proof must differ from original upload');
    fs.writeFileSync(path.join(output, 'selected.png'), selectedBytes);
    const before = await get('profilephotoworkflow/entitlements');
    const creditsBefore = (await get('credit/status')).credits;
    const data = { imageStoragePath: selected.storagePath, style: 'linkedin', packageCode: 'pro_package',
      numOutputs: 1, isRegeneration: true, refinementCode: 'upright_posture',
      replacesProcessedImageId: selected.processedImageId, clientRequestId: crypto.randomUUID() };
    fs.writeFileSync(path.join(output, 'request.private.json'), JSON.stringify(data), { mode: 0o600 });
    const result = await post(data);
    assert.equal(count(), startCount + 2, 'Posture edit must invoke fixture exactly once');
    const capture = JSON.parse(dockerRead('capture.json'));
    assert.equal(capture.posture, true);
    assert.equal(capture.model, 'gpt-image-2');
    for (const instruction of ['Edit the supplied selected proof, not a new portrait.',
      'Gently straighten shoulder posture.', 'Keep the head angle, facial expression and camera viewpoint unchanged.',
      'Keep clothing, hair, background, lighting, framing and all other details unchanged.']) {
      assert.ok(capture.prompt.includes(instruction), 'Provider did not receive posture/preservation instruction');
    }
    for (const name of ['input.png', 'output.png']) {
      cp.execFileSync('docker', ['cp', `aipmverify-provider-fixture-1:/evidence/posture-chain/${name}`, path.join(output, name)], { stdio: 'pipe' });
    }
    assert.equal(capture.inputHash, hash(fs.readFileSync(path.join(output, 'input.png'))));
    const saved = await image(result.storagePath);
    fs.writeFileSync(path.join(output, 'saved.png'), saved);
    assert.equal(hash(saved), capture.outputHash, 'Saved bytes differ from actual provider response');
    assert.equal(hash(saved), hash(fs.readFileSync(path.join(output, 'response.png'))));
    assert.notEqual(hash(saved), hash(selectedBytes), 'Fixture output must be distinct');
    const pixels = JSON.parse(cp.execFileSync('dotnet', [probe, 'verify', path.join(output, 'selected.png'),
      path.join(output, 'input.png'), path.join(output, 'output.png'), path.join(output, 'saved.png')], { encoding: 'utf8' }));
    const resultRow = imageRow(result.processedImageId);
    assert.notEqual(resultRow.Id, selectedRow.Id);
    assert.equal(resultRow.UserProfileId, selectedRow.UserProfileId);
    assert.equal(resultRow.ReplacesProcessedImageId, selectedRow.Id);
    assert.equal(resultRow.OriginalImageUrl, selectedRow.OriginalImageUrl);
    assert.equal(resultRow.ProcessedImageUrl, result.storagePath);
    assert.ok(resultRow.PromptVersion.includes('refinement-v1:upright_posture'));
    assert.deepEqual(imageRow(selected.processedImageId), selectedRow, 'Selected source row changed');
    assert.equal(hash(await image(selected.storagePath)), hash(selectedBytes), 'Selected source bytes changed');
    const replay = await post(data);
    assert.equal(replay.processedImageId, result.processedImageId);
    assert.equal(replay.storagePath, result.storagePath);
    assert.equal(count(), startCount + 2, 'Replay invoked provider');
    assert.deepEqual(imageRow(result.processedImageId), resultRow, 'Replay changed result lineage');
    assert.deepEqual(imageRow(selected.processedImageId), selectedRow, 'Replay changed source lineage');
    const replacements = rows(`SELECT COUNT(*) AS Count FROM ProcessedImages i WHERE ${profileFilter} AND i.ReplacesProcessedImageId=${Number(selected.processedImageId)}`);
    assert.equal(replacements[0].Count, 1, 'Replay created another replacement row');
    const after = await get('profilephotoworkflow/entitlements');
    const sum = (items, key) => items.reduce((n, item) => n + (item[key] || 0), 0);
    assert.equal(sum(before, 'remainingRefinements') - sum(after, 'remainingRefinements'), 1);
    for (const key of ['remainingCandidates', 'remainingPremiumAugmentations']) assert.equal(sum(after, key), sum(before, key));
    assert.equal((await get('credit/status')).credits, creditsBefore);
    const evidence = { localOnly: true, provider: 'deterministic fixture, not AI quality evaluation',
      ...pixels, sourceRowPreserved: true, sourceBytesPreserved: true, lineageAndReplacementVerified: true,
      posturePromptReachedProvider: true, savedBytesHash: hash(saved), providerResponseBytesHash: capture.outputHash,
      postureProviderCalls: 1, replayProviderCalls: 0, refinementDebit: 1, otherBalancesUnchanged: true };
    fs.writeFileSync(path.join(output, 'verified-chain.json'), JSON.stringify(evidence, null, 2));
    console.log(JSON.stringify(evidence, null, 2));
  } finally { await api.dispose(); }
})().catch(error => { console.error('Local posture-chain check failed: ' + error.message); process.exitCode = 1; });
