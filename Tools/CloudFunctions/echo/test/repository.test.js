'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { createWxRepository, boundedTransaction, reportDiagnostic } = require('../repository');
const { createMain } = require('../index');
const { ProtocolError } = require('../protocol');

test('diagnostic output uses only fixed fields and never emits raw error or request data', () => {
  const entries = [];
  const secret = 'private-openid/token/payload';
  reportDiagnostic((entry) => entries.push(entry), 'database.get', {
    name: 'CloudSDKError', errCode: -502001, code: 'DATABASE_REQUEST_FAILED',
    errMsg: 'collection does not exist ' + secret, stack: secret, event: secret, OPENID: secret
  });
  assert.deepEqual(entries[0], { stage: 'database.get', code: 'DATABASE_REQUEST_FAILED',
    errCode: -502001, errorName: 'CloudSDKError', reason: 'configuration' });
  reportDiagnostic((entry) => entries.push(entry), secret, {
    name: secret, code: secret, errCode: { token: secret }, message: secret
  });
  assert.deepEqual(entries[1], { stage: 'unknown', code: 'unknown', errCode: 'unknown',
    errorName: 'unknown', reason: 'unknown' });
  assert.equal(JSON.stringify(entries).includes(secret), false);
  assert.doesNotThrow(() => reportDiagnostic(() => { throw new Error(secret); }, 'database.get', {}));
});

test('diagnostic reasons are bounded classifications even when SDK messages contain private data', () => {
  for (const [error, reason] of [
    [{ code: 'MODULE_NOT_FOUND', message: 'private path' }, 'missing_module'],
    [{ code: 'ETIMEDOUT' }, 'timeout'],
    [{ code: 'INVALID_PARAM' }, 'invalid_argument'],
    [{ message: 'permission denied private-user' }, 'permission'],
    [{ code: 'DATABASE_TRANSACTION_CONFLICT' }, 'transaction_conflict'],
    [{ code: 'ECONNRESET' }, 'network'],
    [new TypeError('private data'), 'runtime']
  ]) {
    const entries = [];
    reportDiagnostic((entry) => entries.push(entry), 'database.get', error);
    assert.equal(entries.length, 1);
    assert.equal(entries[0].reason, reason);
  }
});

test('initialization failures identify their stage without changing the client error protocol', async () => {
  const event = { apiVersion: 1, action: 'get', payloadJson: 'private-payload', openid: 'private-openid' };
  for (const failingStage of ['sdk.load', 'sdk.init', 'database.init', 'context.read']) {
    const entries = [];
    const fail = () => { throw Object.assign(new Error('private-payload private-openid'), { code: 'SDK_TEST_FAILURE' }); };
    const cloud = {
      init: failingStage === 'sdk.init' ? fail : () => {},
      database: failingStage === 'database.init' ? fail : () => ({}),
      getWXContext: failingStage === 'context.read' ? fail : () => ({ OPENID: 'test-owner' })
    };
    const main = createMain({ loadCloud: failingStage === 'sdk.load' ? fail : () => cloud,
      diagnostic: (entry) => entries.push(entry) });
    assert.deepEqual(await main(event), { ok: false, apiVersion: 1,
      error: { code: 'TRANSIENT_UNAVAILABLE', retryable: true } });
    assert.equal(entries.length, 1);
    assert.equal(entries[0].stage, failingStage);
    assert.equal(entries[0].code, 'SDK_TEST_FAILURE');
    assert.equal(JSON.stringify(entries).includes('private-'), false);
  }
});

test('database and transaction diagnostics retain the failing operation before error mapping', async () => {
  const failure = Object.assign(new Error('private-details'), { code: 'SDK_TEST_FAILURE' });
  const entries = [];
  const diagnostic = (entry) => entries.push(entry);
  const document = { get: async () => { throw failure; }, set: async () => { throw failure; } };
  const transaction = { collection: () => ({ doc: () => document }),
    commit: async () => { throw failure; }, rollback: async () => ({}) };
  const database = { collection: transaction.collection, startTransaction: async () => transaction };
  const repository = createWxRepository(database, { diagnostic });
  await assert.rejects(repository.get('echo_shadows', 'private-id'), { code: 'TRANSIENT_UNAVAILABLE' });
  await assert.rejects(repository.transaction((tx) => tx.get('echo_shadows', 'private-id')), { code: 'TRANSIENT_UNAVAILABLE' });
  await assert.rejects(repository.transaction((tx) => tx.set('echo_shadows', 'private-id', {})), { code: 'TRANSIENT_UNAVAILABLE' });
  await assert.rejects(repository.transaction(async () => 'receipt'), { code: 'TRANSIENT_UNAVAILABLE' });
  database.startTransaction = async () => { throw failure; };
  await assert.rejects(repository.transaction(async () => 'receipt'), { code: 'TRANSIENT_UNAVAILABLE' });
  for (const stage of ['database.get', 'transaction.get', 'transaction.set', 'transaction.commit', 'transaction.start']) {
    assert.ok(entries.some((entry) => entry.stage === stage && entry.code === 'SDK_TEST_FAILURE'), stage);
  }
  assert.equal(JSON.stringify(entries).includes('private-'), false);
});

test('a failing diagnostic sink cannot alter client errors or add transaction attempts', async () => {
  let starts = 0;
  const diagnostic = () => { throw new Error('logger offline'); };
  await assert.rejects(boundedTransaction(async () => {
    starts++;
    throw { code: 'DATABASE_TRANSACTION_CONFLICT' };
  }, async () => {}, { diagnostic }), { code: 'TRANSIENT_UNAVAILABLE', retryable: true });
  assert.equal(starts, 2);
  const repository = createWxRepository({ collection: () => { throw new Error('permission denied'); } }, { diagnostic });
  await assert.rejects(repository.get('echo_shadows', 'id'), { code: 'PERMISSION_DENIED', retryable: false });
});

test('transaction wall-clock budget prevents a late commit and a third attempt', async () => {
  let time = 0;
  let starts = 0;
  let commits = 0;
  let rollbacks = 0;
  const start = async () => {
    starts++;
    return { commit: async () => { commits++; }, rollback: async () => { rollbacks++; } };
  };
  await assert.rejects(boundedTransaction(start, async () => { time = 4000; }, { clock: () => time }),
    { code: 'TRANSIENT_UNAVAILABLE', retryable: true });
  assert.equal(starts, 1);
  assert.equal(commits, 0);
  assert.equal(rollbacks, 1);
});

test('an ambiguous commit response is not retried inside the adapter', async () => {
  let starts = 0;
  await assert.rejects(boundedTransaction(async () => {
    starts++;
    return { commit: async () => { throw new Error('timeout after commit'); }, rollback: async () => ({}) };
  }, async () => 'receipt'), { code: 'TRANSIENT_UNAVAILABLE' });
  assert.equal(starts, 1);
});

test('business errors roll back and are never automatically retried', async () => {
  let starts = 0;
  let rollbacks = 0;
  await assert.rejects(boundedTransaction(async () => {
    starts++;
    return { commit: async () => ({}), rollback: async () => { rollbacks++; } };
  }, async () => { throw new ProtocolError('CHALLENGE_CONFLICT'); }), { code: 'CHALLENGE_CONFLICT', retryable: false });
  assert.equal(starts, 1);
  assert.equal(rollbacks, 1);
});

test('SDK errors map to small protocol errors, not successful empty results', async () => {
  const database = { collection: () => ({ doc: () => ({ get: async () => {
    throw { errMsg: 'document.get:fail permission denied private request-id' };
  } }) }) };
  const repository = createWxRepository(database);
  await assert.rejects(repository.get('echo_shadows', 'id'), { code: 'PERMISSION_DENIED', retryable: false });
  database.collection = () => ({ doc: () => ({ get: async () => {
    throw { errMsg: 'document.get:fail collection echo_shadows does not exist' };
  } }) });
  await assert.rejects(repository.get('echo_shadows', 'id'), { code: 'CLOUD_NOT_CONFIGURED', retryable: false });
});

let cloud;
try { cloud = require('wx-server-sdk'); } catch { /* Pure handler tests also run without npm install. */ }

test('production entry initializes the real SDK and refreshes context with no credentials or network',
  { skip: !cloud }, () => {
    const { spawnSync } = require('node:child_process');
    const result = spawnSync(process.execPath, ['-e', `
      let networkCalls = 0;
      for (const moduleName of ['node:http', 'node:https']) {
        const transport = require(moduleName);
        transport.request = transport.get = () => { networkCalls++; throw new Error('network forbidden in test'); };
      }
      const logs = [];
      console.warn = (...args) => logs.push(args);
      const { main } = require('./index');
      (async () => {
        const authenticated = await main({ apiVersion: 0, action: 'get' });
        delete process.env.WX_OPENID;
        const unauthenticated = await main({ apiVersion: 1, action: 'get', openid: 'forged-owner' });
        console.log(JSON.stringify({ authenticated, unauthenticated, networkCalls,
          diagnostics: logs.filter((entry) => entry[0] === 'echo.diagnostic') }));
      })().catch(() => { process.exitCode = 1; });
    `], {
      cwd: require('node:path').resolve(__dirname, '..'), encoding: 'utf8', timeout: 5000,
      // Do not inherit cloud credentials or developer account context into this child.
      env: { WX_CONTEXT_KEYS: 'WX_OPENID', WX_OPENID: 'unit-test-owner', TCB_ENV: 'echo-unit-test-only' }
    });
    assert.equal(result.status, 0, result.stderr);
    const output = JSON.parse(result.stdout);
    assert.deepEqual(output.authenticated, { ok: false, apiVersion: 1,
      error: { code: 'API_VERSION_UNSUPPORTED', retryable: false } });
    assert.deepEqual(output.unauthenticated, { ok: false, apiVersion: 1,
      error: { code: 'UNAUTHENTICATED', retryable: false } });
    assert.equal(output.networkCalls, 0);
    assert.deepEqual(output.diagnostics, []);
  });

// Use the installed wx-server-sdk wrapper, replace only its lower-level database I/O.
// No get/commit/query reaches a cloud environment or attempts authentication.
test('installed SDK 3.0.1 accepts the adapter signatures and unwraps normal/transaction documents',
  { skip: !cloud }, async () => {
    assert.equal(require('wx-server-sdk/package.json').version, '3.0.1');
    cloud.init({ env: 'echo-unit-test-only', timeout: 4000, retries: 0 });
    const database = cloud.database({ throwOnNotFound: false });
    assert.equal(database._db.config.timeout, 4000);
    assert.equal(database._db.config.retries, 0);
    const documents = new Map();
    const calls = [];
    function collection(name, pending, inTransaction = false) {
      const query = {
        doc(id) {
          const key = name + '/' + id;
          return {
            async get() {
              const value = (pending && pending.get(key)) || documents.get(key);
              return { data: inTransaction ? value || null : value ? [value] : [] };
            },
            async set(data) {
              assert.equal(Object.hasOwn(data, 'data'), false, 'wx wrapper must unwrap options.data');
              calls.push(['set', name, id]);
              pending.set(key, { ...data, _id: id });
              return { updated: 0, upsertedId: id };
            }
          };
        },
        where(filter) { calls.push(['where', filter]); return query; },
        orderBy(field, direction) { calls.push(['orderBy', field, direction]); return query; },
        field(projection) { calls.push(['field', projection]); return query; },
        limit(limit) { calls.push(['limit', limit]); return query; },
        async get() { return { data: [{ _id: 'score', schemaVersion: 1 }] }; }
      };
      return query;
    }
    database._db.collection = (name) => collection(name);
    database._db.startTransaction = async () => {
      const pending = new Map();
      return {
        collection: (name) => collection(name, pending, true),
        async commit() { for (const [key, value] of pending) documents.set(key, value); return {}; },
        async rollback() { pending.clear(); return {}; }
      };
    };
    const repository = createWxRepository(database);
    assert.equal(await repository.get('echo_shadows', 'missing'), null);
    const result = await repository.transaction(async (tx) => {
      assert.equal(await tx.get('echo_results', 'r1'), null);
      await tx.set('echo_results', 'r1', { schemaVersion: 1, challengeId: 'challenge-1' });
      return 'receipt';
    });
    assert.equal(result, 'receipt');
    assert.equal((await repository.get('echo_results', 'r1')).challengeId, 'challenge-1');
    assert.deepEqual(await repository.leaderboard('board', 7), [{ _id: 'score', schemaVersion: 1 }]);
    assert.deepEqual(calls.filter((call) => call[0] === 'orderBy'), [
      ['orderBy', 'distanceMeters', 'desc'], ['orderBy', 'playerLeadMeters', 'desc'],
      ['orderBy', 'achievedAt', 'asc'], ['orderBy', '_id', 'asc']
    ]);
    assert.deepEqual(calls.find((call) => call[0] === 'where'), ['where', { boardId: 'board' }]);
    assert.deepEqual(calls.find((call) => call[0] === 'limit'), ['limit', 7]);
    const fields = calls.find((call) => call[0] === 'field')[1];
    assert.equal(fields.payloadJson, undefined);
    assert.equal(fields.challengerOpenid, true, 'only used inside the handler to compute isMe');
  });
