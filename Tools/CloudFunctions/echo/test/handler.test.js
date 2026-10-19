'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createHandler } = require('../handler');
const { COLLECTIONS, storageKey, validatePayload, MAX_PAYLOAD_BYTES } = require('../protocol');
const { FakeRepository } = require('./fake-repository');

const fixturePath = path.join(__dirname, 'fixtures/identity-v1.json');
const payloadJson = fs.readFileSync(fixturePath, 'utf8').trim();
const identity = JSON.parse(payloadJson);
const ctx = (OPENID = 'owner-test') => ({ OPENID });
const request = (action, fields = {}) => ({ apiVersion: 1, action, ...fields });
const publishRequest = (fields = {}) => request('publish', { payloadJson, rulesVersion: 1, ...fields });
const reportRequest = (boardId, fields = {}) => request('report', { boardId, challengeId: 'challenge-1',
  rulesVersion: 1, distanceMeters: 500, playerLeadMeters: 3, endReason: 'finish_reached', playerWon: true, ...fields });

function setup(options = {}) {
  const repository = new FakeRepository();
  let time = 1700000000000;
  let seed = 100;
  const handle = createHandler({ repository, clock: () => ++time, seedFactory: () => ++seed, ...options });
  return { repository, handle };
}

async function publish(handle, fields = {}, context = ctx()) {
  const result = await handle(publishRequest(fields), context);
  assert.equal(result.ok, true, JSON.stringify(result));
  return result.data;
}

function expectError(result, code) {
  assert.equal(result.ok, false);
  assert.equal(result.error.code, code);
}

test('four actions reject bad dispatch/version/context without trusting event.openid', async () => {
  const { handle, repository } = setup();
  expectError(await handle(publishRequest({ openid: 'forged' }), {}), 'UNAUTHENTICATED');
  expectError(await handle(request('constructor'), ctx()), 'INVALID_ARGUMENT');
  expectError(await handle(request('missing'), ctx()), 'INVALID_ARGUMENT');
  expectError(await handle(publishRequest({ apiVersion: 2 }), ctx()), 'API_VERSION_UNSUPPORTED');
  const data = await publish(handle, { openid: 'forged', ownerOpenid: 'forged', identityId: 'forged', generation: 999 });
  assert.equal(data.inviterOpenid, 'owner-test');
  assert.equal(data.identityId, identity.identityId);
  assert.equal(data.generation, identity.generation);
  assert.equal(repository.all(COLLECTIONS.shadows)[0].ownerOpenid, 'owner-test');
});

test('immutable publishing preserves exact payload and old seed; no score is stored in a shadow', async () => {
  const { handle, repository } = setup();
  const first = await publish(handle);
  assert.deepEqual(await publish(handle), first);
  const different = JSON.parse(payloadJson);
  different.pace += 1;
  expectError(await handle(publishRequest({ payloadJson: JSON.stringify(different) }), ctx()), 'SNAPSHOT_CONFLICT');
  const stored = repository.all(COLLECTIONS.shadows);
  assert.equal(stored.length, 1);
  assert.equal(stored[0].payloadJson, payloadJson);
  assert.equal(Object.hasOwn(stored[0], 'bestDistance'), false);
});

test('concurrent initial publishes converge on the committed seed', async () => {
  const { handle, repository } = setup();
  repository.synchronizeNextCommits();
  const results = await Promise.all([publish(handle), publish(handle)]);
  assert.deepEqual(results[0], results[1]);
  assert.equal(repository.all(COLLECTIONS.shadows).length, 1);
  assert.equal(repository.conflicts, 1);
});

test('owner/id/rules triples distinguish boards and old snapshots remain fetchable', async () => {
  const { handle, repository } = setup({ supportedRules: [1, 2] });
  const original = await publish(handle);
  const otherOwner = await publish(handle, {}, ctx('other-owner'));
  const otherRules = await publish(handle, { rulesVersion: 2 });
  assert.equal(new Set([original.boardId, otherOwner.boardId, otherRules.boardId]).size, 3);
  const next = JSON.parse(payloadJson);
  next.identityId = 'echo-next'; next.memoryContract.identityId = next.identityId;
  await publish(handle, { payloadJson: JSON.stringify(next) });
  const found = await handle(request('get', { inviterOpenid: 'owner-test', identityId: identity.identityId, rulesVersion: 1 }), ctx('friend'));
  assert.equal(found.data.payloadJson, payloadJson);
  assert.equal(found.data.boardId, original.boardId);
  const missing = await handle(request('get', { inviterOpenid: 'missing-owner', identityId: identity.identityId, rulesVersion: 1 }), ctx());
  assert.deepEqual(missing.data, { found: false });
  assert.equal(repository.all(COLLECTIONS.shadows).length, 4);
});

test('first-time challenger needs no own shadow and cannot write inviter score', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  const result = await handle(reportRequest(board.boardId, { openid: 'owner-test' }), ctx('friend'));
  assert.equal(result.ok, true);
  assert.equal(result.data.eligible, true);
  assert.equal(repository.all(COLLECTIONS.shadows).length, 1);
  assert.equal(repository.all(COLLECTIONS.scores)[0].challengerOpenid, 'friend');
});

test('concurrent duplicate report creates one result and returns the same receipt', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  repository.synchronizeNextCommits();
  const results = await Promise.all([handle(reportRequest(board.boardId), ctx('friend')), handle(reportRequest(board.boardId), ctx('friend'))]);
  assert.ok(results.every((value) => value.ok));
  assert.equal(results[0].data.receiptId, results[1].data.receiptId);
  assert.equal(results[0].data.acceptedAt, results[1].data.acceptedAt);
  assert.deepEqual(results.map((value) => value.data.duplicate).sort(), [false, true]);
  assert.equal(repository.all(COLLECTIONS.results).length, 1);
  assert.equal(repository.all(COLLECTIONS.scores).length, 1);
  assert.equal(repository.conflicts, 1);
});

test('challenge deduplication covers changed scores, board and rules; ownership is separate', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  assert.equal((await handle(reportRequest(board.boardId), ctx('friend'))).ok, true);
  for (const change of [{ distanceMeters: 999 }, { boardId: 'different-board' }, { rulesVersion: 999 }]) {
    expectError(await handle(reportRequest(board.boardId, change), ctx('friend')), 'CHALLENGE_CONFLICT');
  }
  assert.equal((await handle(reportRequest(board.boardId), ctx('second-friend'))).ok, true);
  assert.equal(repository.all(COLLECTIONS.results).length, 2);
});

test('failure after staged result rolls back both documents; same report can succeed later', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  repository.failOnSet = COLLECTIONS.scores;
  const failed = await handle(reportRequest(board.boardId), ctx('friend'));
  expectError(failed, 'TRANSIENT_UNAVAILABLE');
  assert.equal(failed.error.retryable, true);
  assert.equal(repository.all(COLLECTIONS.results).length, 0);
  assert.equal(repository.all(COLLECTIONS.scores).length, 0);
  assert.equal((await handle(reportRequest(board.boardId), ctx('friend'))).ok, true);
});

test('transaction conflicts retry at most once and never partially commit', async () => {
  const { handle, repository } = setup();
  repository.forcedConflicts = 5;
  expectError(await handle(publishRequest(), ctx()), 'TRANSIENT_UNAVAILABLE');
  assert.equal(repository.attempts, 2);
  assert.equal(repository.all(COLLECTIONS.shadows).length, 0);
  assert.equal(repository.rollbacks, 2);
});

test('competing challenges do not lose the best update or mix fields from different runs', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  repository.synchronizeNextCommits();
  const answers = await Promise.all([
    handle(reportRequest(board.boardId, { challengeId: 'a', distanceMeters: 500, playerLeadMeters: 99 }), ctx('friend')),
    handle(reportRequest(board.boardId, { challengeId: 'b', distanceMeters: 600, playerLeadMeters: -3, playerWon: false }), ctx('friend'))
  ]);
  assert.ok(answers.every((answer) => answer.ok));
  const score = repository.all(COLLECTIONS.scores)[0];
  assert.equal(score.distanceMeters, 600);
  assert.equal(score.playerLeadMeters, -3);
  assert.equal(score.playerWon, false);
  assert.equal(score.bestChallengeId, 'b');
  assert.equal(repository.all(COLLECTIONS.results).length, 2);
});

test('same-player ties use time then challengeId, including a concurrent commit conflict', async () => {
  const { handle, repository } = setup({ clock: () => 1000 });
  const board = await publish(handle);
  repository.synchronizeNextCommits();
  await Promise.all([
    handle(reportRequest(board.boardId, { challengeId: 'z' }), ctx('friend')),
    handle(reportRequest(board.boardId, { challengeId: 'a' }), ctx('friend'))
  ]);
  assert.equal(repository.all(COLLECTIONS.scores)[0].bestChallengeId, 'a');
  const later = createHandler({ repository, clock: () => 2000 });
  await later(reportRequest(board.boardId, { challengeId: '0' }), ctx('friend'));
  assert.equal(repository.all(COLLECTIONS.scores)[0].bestChallengeId, 'a');
});

test('abandoned results are idempotently stored but never ranked', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  const result = await handle(reportRequest(board.boardId, { endReason: 'abandoned', playerWon: false }), ctx('friend'));
  assert.equal(result.data.eligible, false);
  assert.equal(repository.all(COLLECTIONS.results).length, 1);
  assert.equal(repository.all(COLLECTIONS.scores).length, 0);
});

test('rule retirement rejects new work but keeps previous receipts and historical leaderboards', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  const first = await handle(reportRequest(board.boardId), ctx('friend'));
  const retired = createHandler({ repository, supportedRules: [], retiredRules: [1] });
  expectError(await retired(publishRequest(), ctx()), 'RULES_RETIRED');
  expectError(await retired(reportRequest(board.boardId, { challengeId: 'new' }), ctx('friend')), 'RULES_RETIRED');
  const duplicate = await retired(reportRequest(board.boardId), ctx('friend'));
  assert.equal(duplicate.data.receiptId, first.data.receiptId);
  assert.equal(duplicate.data.acceptedAt, first.data.acceptedAt);
  assert.equal(duplicate.data.duplicate, true);
  const history = await retired(request('leaderboard', { boardId: board.boardId }), ctx('friend'));
  assert.equal(history.ok, true);
  assert.equal(history.data.items.length, 1);
});

test('leaderboard separates empty/missing/errors and rejects malformed limit', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  assert.deepEqual((await handle(request('leaderboard', { boardId: board.boardId }), ctx())).data.items, []);
  expectError(await handle(request('leaderboard', { boardId: 'missing-board' }), ctx()), 'BOARD_NOT_FOUND');
  expectError(await handle(request('leaderboard', { boardId: board.boardId, limit: '2' }), ctx()), 'INVALID_ARGUMENT');
  repository.leaderboard = async () => { throw new Error('Do not leak user-secret'); };
  const failed = await handle(request('leaderboard', { boardId: board.boardId }), ctx());
  expectError(failed, 'TRANSIENT_UNAVAILABLE');
  assert.equal(JSON.stringify(failed).includes('user-secret'), false);
});

test('leaderboard orders distance/lead/time/id before top-N and projects only public fields', async () => {
  const { handle, repository } = setup({ clock: () => 1000 });
  const board = await publish(handle);
  const otherBoard = await publish(handle, {}, ctx('other-owner'));
  for (const [user, distance, lead] of [['alpha', 700, 1], ['bravo', 700, 2], ['charlie', 600, 99], ['delta', 700, 2]]) {
    assert.equal((await handle(reportRequest(board.boardId, { distanceMeters: distance, playerLeadMeters: lead }), ctx(user))).ok, true);
  }
  await handle(reportRequest(otherBoard.boardId, { distanceMeters: 9999 }), ctx('outsider'));
  const all = await handle(request('leaderboard', { boardId: board.boardId, limit: 999 }), ctx('bravo'));
  assert.equal(all.data.items.length, 4);
  const top = all.data.items;
  assert.equal(top[0].playerLeadMeters, 2);
  assert.equal(top[1].playerLeadMeters, 2);
  assert.ok(top[0].entryId < top[1].entryId);
  assert.equal(top[2].playerLeadMeters, 1);
  assert.equal(top[3].distanceMeters, 600);
  assert.equal(top.filter((item) => item.isMe).length, 1);
  assert.deepEqual(Object.keys(top[0]).sort(), ['rank', 'entryId', 'displayLabel', 'isMe', 'distanceMeters', 'playerLeadMeters', 'playerWon'].sort());
  const limited = await handle(request('leaderboard', { boardId: board.boardId, limit: 2 }), ctx());
  assert.deepEqual(limited.data.items, top.slice(0, 2).map((item) => ({ ...item, isMe: false })));
  assert.equal((await handle(request('leaderboard', { boardId: board.boardId, limit: 0 }), ctx())).data.items.length, 1);
  assert.equal(repository.all(COLLECTIONS.scores).length, 5);
});

test('unknown schemas and corrupted stored metrics fail rather than freezing or showing a bad board', async () => {
  const { handle, repository } = setup();
  const board = await publish(handle);
  await handle(reportRequest(board.boardId), ctx('friend'));
  const score = repository.all(COLLECTIONS.scores)[0];
  repository.put(COLLECTIONS.scores, score._id, { ...score, distanceMeters: NaN });
  expectError(await handle(reportRequest(board.boardId, { challengeId: 'next' }), ctx('friend')), 'PAYLOAD_INVALID');
  expectError(await handle(request('leaderboard', { boardId: board.boardId }), ctx()), 'PAYLOAD_INVALID');
  const savedBoard = repository.all(COLLECTIONS.shadows)[0];
  repository.put(COLLECTIONS.shadows, board.boardId, { ...savedBoard, schemaVersion: 999 });
  expectError(await handle(request('get', { inviterOpenid: 'owner-test', identityId: identity.identityId, rulesVersion: 1 }), ctx()), 'SCHEMA_UNSUPPORTED');
});

test('report rejects invalid metrics, invalid victory semantics and nonexistent boards', async () => {
  const { handle } = setup();
  const board = await publish(handle);
  for (const change of [{ distanceMeters: -1 }, { distanceMeters: Infinity }, { playerLeadMeters: NaN },
    { distanceMeters: 10000001 }, { endReason: 'collision' }, { playerWon: 'true' }, { rulesVersion: 1.1 }]) {
    expectError(await handle(reportRequest(board.boardId, change), ctx()), 'INVALID_ARGUMENT');
  }
  expectError(await handle(reportRequest('no-board'), ctx()), 'BOARD_NOT_FOUND');
});

test('payload bytes are UTF-8 bounded at exactly 16KiB and input is not rewritten', () => {
  const sample = JSON.parse(payloadJson);
  sample.unknownNote = '影';
  const base = JSON.stringify(sample);
  const exact = base + ' '.repeat(MAX_PAYLOAD_BYTES - Buffer.byteLength(base, 'utf8'));
  assert.equal(Buffer.byteLength(exact, 'utf8'), MAX_PAYLOAD_BYTES);
  assert.equal(validatePayload(exact).identityId, identity.identityId);
  assert.throws(() => validatePayload(exact + ' '), { code: 'PAYLOAD_TOO_LARGE' });
  assert.equal(validatePayload(payloadJson).identityId, identity.identityId);
});

test('payload rejects missing/future/nested versions, shapes and values normalized by Unity', () => {
  for (const mutate of [
    (v) => { delete v.version; }, (v) => { v.version = 2; },
    (v) => { v.style.version = 99; }, (v) => { v.memoryContract.version = 2; }
  ]) {
    const value = JSON.parse(payloadJson); mutate(value);
    assert.throws(() => validatePayload(JSON.stringify(value)), { code: 'PAYLOAD_VERSION_UNSUPPORTED' });
  }
  for (const mutate of [
    (v) => { v.policyWeights.pop(); }, (v) => { v.policyWeights[0] = 5; },
    (v) => { v.sequenceTransitions[0] = -1; }, (v) => { v.pace = 0; },
    (v) => { v.generation = 1.5; }, (v) => { v.sequencePairCount = 2147483648; },
    (v) => { v.style.lanePreference = 3; }, (v) => { v.memoryContract.identityId = 'wrong'; },
    (v) => { v.identityId = 'x/unsafe'; }, (v) => { v.style.jumpActionSamples = 3; v.style.verticalActionSamples = 0; }
  ]) {
    const value = JSON.parse(payloadJson); mutate(value);
    assert.throws(() => validatePayload(JSON.stringify(value)), { code: 'PAYLOAD_INVALID' });
  }
  const calibration = JSON.parse(payloadJson); calibration.memoryContract.evidenceCount = 2;
  assert.throws(() => validatePayload(JSON.stringify(calibration)), { code: 'IDENTITY_NOT_CHALLENGE_READY' });
  assert.throws(() => validatePayload(payloadJson.replace(/"pace":\s*[\d.]+/, '"pace":1e400')), { code: 'PAYLOAD_INVALID' });
  for (const malformed of ['null', '[]', '{broken']) assert.throws(() => validatePayload(malformed), { code: 'PAYLOAD_INVALID' });
});

test('storage key tuples are unambiguous and domain separated without identity hashing', () => {
  assert.notEqual(storageKey('board', 'ab', 'c', 1), storageKey('board', 'a', 'bc', 1));
  assert.notEqual(storageKey('result', 'a', 'b'), storageKey('score', 'a', 'b'));
});
