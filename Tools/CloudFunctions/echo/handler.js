'use strict';

const crypto = require('node:crypto');
const { API_VERSION, SCHEMA_VERSION, INT_MAX, COLLECTIONS, requireThat,
  object, identifier, integer, validatePayload, storageKey, schema, reportInput,
  sameReport, compareBest, validateScore, errorResponse } = require('./protocol');

function createHandler({ repository, clock = Date.now, seedFactory = () => crypto.randomInt(1, INT_MAX + 1),
  supportedRules = [1], retiredRules = [] }) {
  const active = new Set(supportedRules);
  const retired = new Set(retiredRules);

  function requireRules(rulesVersion) {
    requireThat(integer(rulesVersion, 1));
    requireThat(!retired.has(rulesVersion), 'RULES_RETIRED');
    requireThat(active.has(rulesVersion), 'RULES_UNSUPPORTED');
  }

  function now() {
    const value = clock();
    requireThat(Number.isSafeInteger(value) && value >= 0, 'TRANSIENT_UNAVAILABLE');
    return value;
  }

  function validateBoard(board, boardId, validateIdentity = true) {
    requireThat(board !== null, 'BOARD_NOT_FOUND');
    schema(board);
    requireThat(identifier(board.ownerOpenid) && identifier(board.identityId)
      && integer(board.rulesVersion, 1) && integer(board.runSeed, 1)
      && boardId === storageKey('board', board.ownerOpenid, board.identityId, board.rulesVersion), 'PAYLOAD_INVALID');
    if (validateIdentity) {
      const identity = validatePayload(board.payloadJson);
      requireThat(board.payloadVersion === identity.version && board.identityId === identity.identityId
        && board.generation === identity.generation && board.pace === identity.pace, 'PAYLOAD_INVALID');
    }
  }

  function published(board, boardId) {
    return { boardId, inviterOpenid: board.ownerOpenid, identityId: board.identityId,
      generation: board.generation, rulesVersion: board.rulesVersion, runSeed: board.runSeed };
  }

  async function publish(event, openid) {
    requireRules(event.rulesVersion);
    const identity = validatePayload(event.payloadJson);
    const boardId = storageKey('board', openid, identity.identityId, event.rulesVersion);
    return repository.transaction(async (tx) => {
      const existing = await tx.get(COLLECTIONS.shadows, boardId);
      if (existing !== null) {
        validateBoard(existing, boardId);
        requireThat(existing.payloadJson === event.payloadJson, 'SNAPSHOT_CONFLICT');
        return published(existing, boardId);
      }
      const runSeed = seedFactory();
      requireThat(integer(runSeed, 1), 'TRANSIENT_UNAVAILABLE');
      const board = { schemaVersion: SCHEMA_VERSION, ownerOpenid: openid,
        identityId: identity.identityId, payloadVersion: identity.version, payloadJson: event.payloadJson,
        generation: identity.generation, pace: identity.pace, rulesVersion: event.rulesVersion,
        runSeed, createdAt: now() };
      await tx.set(COLLECTIONS.shadows, boardId, board);
      return published(board, boardId);
    });
  }

  async function get(event) {
    requireRules(event.rulesVersion);
    requireThat(identifier(event.inviterOpenid) && identifier(event.identityId));
    const boardId = storageKey('board', event.inviterOpenid, event.identityId, event.rulesVersion);
    const board = await repository.get(COLLECTIONS.shadows, boardId);
    if (board === null) return { found: false };
    validateBoard(board, boardId);
    return { found: true, boardId, payloadJson: board.payloadJson, ownerOpenid: board.ownerOpenid,
      identityId: board.identityId, payloadVersion: board.payloadVersion,
      generation: board.generation, rulesVersion: board.rulesVersion, runSeed: board.runSeed };
  }

  async function report(event, openid) {
    const input = reportInput(event);
    const resultId = storageKey('result', openid, input.challengeId);
    return repository.transaction(async (tx) => {
      const existing = await tx.get(COLLECTIONS.results, resultId);
      if (existing !== null) {
        schema(existing);
        requireThat(existing.challengerOpenid === openid && sameReport(existing, input), 'CHALLENGE_CONFLICT');
        requireThat(Number.isSafeInteger(existing.acceptedAt) && existing.acceptedAt >= 0
          && existing.eligible === (input.endReason !== 'abandoned'), 'PAYLOAD_INVALID');
        return { receiptId: resultId, acceptedAt: existing.acceptedAt, duplicate: true, eligible: existing.eligible };
      }
      // A receipt remains readable after its rules retire; no new result is accepted.
      requireRules(input.rulesVersion);
      const board = await tx.get(COLLECTIONS.shadows, input.boardId);
      validateBoard(board, input.boardId);
      requireThat(board.rulesVersion === input.rulesVersion);
      const result = { schemaVersion: SCHEMA_VERSION, challengerOpenid: openid, ...input,
        eligible: input.endReason !== 'abandoned', acceptedAt: now() };
      let scoreId;
      let bestScore;
      if (result.eligible) {
        scoreId = storageKey('score', input.boardId, openid);
        const previous = await tx.get(COLLECTIONS.scores, scoreId);
        if (previous !== null) {
          validateScore(previous);
          requireThat(previous.boardId === input.boardId && previous.challengerOpenid === openid, 'PAYLOAD_INVALID');
        }
        const previousResult = previous && { ...previous,
          acceptedAt: previous.achievedAt, challengeId: previous.bestChallengeId };
        if (previous === null || compareBest(result, previousResult) < 0) {
          bestScore = { schemaVersion: SCHEMA_VERSION,
            boardId: input.boardId, challengerOpenid: openid, bestChallengeId: input.challengeId,
            distanceMeters: result.distanceMeters, playerLeadMeters: result.playerLeadMeters,
            endReason: result.endReason, playerWon: result.playerWon, achievedAt: result.acceptedAt };
        }
      }
      // Finish every read before the first write; both mutations commit together.
      await tx.set(COLLECTIONS.results, resultId, result);
      if (bestScore) await tx.set(COLLECTIONS.scores, scoreId, bestScore);
      return { receiptId: resultId, acceptedAt: result.acceptedAt, duplicate: false, eligible: result.eligible };
    });
  }

  async function leaderboard(event, openid) {
    requireThat(identifier(event.boardId));
    const requestedLimit = event.limit === undefined ? 20 : event.limit;
    requireThat(Number.isSafeInteger(requestedLimit));
    const limit = Math.min(50, Math.max(1, requestedLimit));
    const board = await repository.get(COLLECTIONS.shadows, event.boardId);
    // Viewing history does not require executing old rules or parsing old identities.
    validateBoard(board, event.boardId, false);
    const scores = await repository.leaderboard(event.boardId, limit);
    const items = scores.map((score, index) => {
      validateScore(score);
      requireThat(score.boardId === event.boardId && identifier(score.challengerOpenid)
        && score._id === storageKey('score', event.boardId, score.challengerOpenid), 'PAYLOAD_INVALID');
      return { rank: index + 1, entryId: score._id, displayLabel: '玩家-' + score._id.slice(0, 8),
        isMe: score.challengerOpenid === openid, distanceMeters: score.distanceMeters,
        playerLeadMeters: score.playerLeadMeters, playerWon: score.playerWon };
    });
    return { boardId: event.boardId, rulesVersion: board.rulesVersion, items };
  }

  const actions = { publish, get, report, leaderboard };
  return async function handle(event, context) {
    try {
      requireThat(object(context) && identifier(context.OPENID), 'UNAUTHENTICATED');
      requireThat(object(event));
      requireThat(event.apiVersion === API_VERSION, 'API_VERSION_UNSUPPORTED');
      requireThat(typeof event.action === 'string' && Object.hasOwn(actions, event.action));
      const data = await actions[event.action](event, context.OPENID);
      return { ok: true, apiVersion: API_VERSION, data };
    } catch (error) {
      return errorResponse(error);
    }
  };
}

module.exports = { createHandler };
