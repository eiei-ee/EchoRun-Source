'use strict';

const crypto = require('node:crypto');
const API_VERSION = 1;
const SCHEMA_VERSION = 1;
const MAX_PAYLOAD_BYTES = 16384;
const INT_MAX = 2147483647;
const FLOAT_MAX = 3.4028234663852886e38;
const METRIC_MAX = 10000000;
const COLLECTIONS = Object.freeze({ shadows: 'echo_shadows', results: 'echo_results', scores: 'echo_scores' });

class ProtocolError extends Error {
  constructor(code, retryable = false) {
    super(code);
    this.code = code;
    this.retryable = retryable;
  }
}

function requireThat(condition, code = 'INVALID_ARGUMENT') {
  if (!condition) throw new ProtocolError(code);
}

function object(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function identifier(value, allowEmpty = false) {
  return typeof value === 'string' && value.length <= 128
    && ((allowEmpty && value === '') || /^[A-Za-z0-9_-]+$/.test(value));
}

function integer(value, min = 0, max = INT_MAX) {
  return Number.isInteger(value) && value >= min && value <= max;
}

function finite(value, min = -FLOAT_MAX, max = FLOAT_MAX) {
  return typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max;
}

function version(value, expected, code = 'PAYLOAD_VERSION_UNSUPPORTED') {
  requireThat(integer(value, 1) && value === expected, code);
}

function validatePayload(payloadJson) {
  requireThat(typeof payloadJson === 'string' && payloadJson.length > 0, 'PAYLOAD_INVALID');
  requireThat(Buffer.byteLength(payloadJson, 'utf8') <= MAX_PAYLOAD_BYTES, 'PAYLOAD_TOO_LARGE');
  let value;
  try { value = JSON.parse(payloadJson); } catch { throw new ProtocolError('PAYLOAD_INVALID'); }
  requireThat(object(value), 'PAYLOAD_INVALID');
  version(value.version, 1);
  const valid = (condition) => requireThat(condition, 'PAYLOAD_INVALID');
  valid(integer(value.generation, 1) && identifier(value.identityId));
  valid(identifier(value.parentIdentityId, value.generation === 1));
  valid(value.generation === 1 ? value.parentIdentityId === '' : value.parentIdentityId !== '');
  valid(integer(value.sourceRunSequence));
  valid(Array.isArray(value.policyWeights) && value.policyWeights.length === 40
    && value.policyWeights.every((n) => finite(n, -4, 4)));
  valid(Array.isArray(value.sequenceTransitions) && value.sequenceTransitions.length === 25
    && value.sequenceTransitions.every((n) => finite(n, 0)));
  valid(integer(value.sequencePairCount));
  valid(finite(value.pace, 0) && value.pace > 0 && finite(value.sourceCourseDuration, 0));
  valid(finite(value.clarity, 0, 1));
  valid(object(value.style));
  version(value.style.version, 3);
  for (const key of ['aggressiveness', 'slideFrequency', 'slideOpportunitySuccess', 'rhythmStability', 'recoveryStyle']) {
    valid(finite(value.style[key], 0, 1));
  }
  for (const key of ['jumpTiming', 'lanePreference']) valid(finite(value.style[key], -1, 1));
  for (const key of ['aggressivenessSamples', 'jumpTimingSamples', 'verticalActionSamples',
    'jumpActionSamples', 'slideActionSamples', 'slideOpportunitySamples', 'laneSamples',
    'rhythmSamples', 'recoverySamples']) valid(integer(value.style[key]));
  valid(value.style.jumpActionSamples + value.style.slideActionSamples <= INT_MAX);
  valid(value.style.verticalActionSamples >= value.style.jumpActionSamples + value.style.slideActionSamples);
  requireThat(object(value.memoryContract), 'IDENTITY_NOT_CHALLENGE_READY');
  const memory = value.memoryContract;
  version(memory.version, 1);
  valid(identifier(memory.contractId) && memory.identityId === value.identityId);
  valid(integer(memory.preferredLane, 0, 2) && finite(memory.confidence, 0, 1) && integer(memory.evidenceCount));
  requireThat(memory.evidenceCount >= 3 && memory.confidence >= 0.6, 'IDENTITY_NOT_CHALLENGE_READY');
  return value;
}

// This creates database keys, never validates or recreates an identityId.
function storageKey(kind, ...parts) {
  const hash = crypto.createHash('sha256');
  for (const part of [kind, ...parts]) {
    const bytes = Buffer.from(String(part), 'utf8');
    hash.update(String(bytes.length) + ':');
    hash.update(bytes);
  }
  return hash.digest('hex');
}

function schema(document) {
  requireThat(object(document) && document.schemaVersion === SCHEMA_VERSION, 'SCHEMA_UNSUPPORTED');
}

function reportInput(event) {
  requireThat(identifier(event.boardId) && identifier(event.challengeId) && integer(event.rulesVersion, 1));
  requireThat(finite(event.distanceMeters, 0, METRIC_MAX) && finite(event.playerLeadMeters, -METRIC_MAX, METRIC_MAX));
  requireThat(['finish_reached', 'collision', 'abandoned'].includes(event.endReason));
  requireThat(typeof event.playerWon === 'boolean');
  requireThat(event.playerWon === (event.endReason === 'finish_reached' && event.playerLeadMeters >= 0));
  return {
    boardId: event.boardId, challengeId: event.challengeId, rulesVersion: event.rulesVersion,
    distanceMeters: event.distanceMeters, playerLeadMeters: event.playerLeadMeters,
    endReason: event.endReason, playerWon: event.playerWon
  };
}

function sameReport(previous, candidate) {
  return Object.keys(candidate).every((key) => previous[key] === candidate[key]);
}

function compareText(left, right) {
  return left === right ? 0 : left < right ? -1 : 1;
}

function compareBest(left, right) {
  return right.distanceMeters - left.distanceMeters
    || right.playerLeadMeters - left.playerLeadMeters
    || left.acceptedAt - right.acceptedAt
    || compareText(left.challengeId, right.challengeId);
}

function compareScores(left, right) {
  return right.distanceMeters - left.distanceMeters
    || right.playerLeadMeters - left.playerLeadMeters
    || left.achievedAt - right.achievedAt
    || compareText(left._id, right._id);
}

function validateScore(score) {
  schema(score);
  requireThat(identifier(score.boardId) && identifier(score.challengerOpenid)
    && identifier(score.bestChallengeId) && finite(score.distanceMeters, 0, METRIC_MAX)
    && finite(score.playerLeadMeters, -METRIC_MAX, METRIC_MAX)
    && Number.isSafeInteger(score.achievedAt) && score.achievedAt >= 0
    && ['finish_reached', 'collision'].includes(score.endReason)
    && typeof score.playerWon === 'boolean'
    && score.playerWon === (score.endReason === 'finish_reached' && score.playerLeadMeters >= 0), 'PAYLOAD_INVALID');
}

function errorResponse(error) {
  const known = error instanceof ProtocolError;
  return { ok: false, apiVersion: API_VERSION, error: {
    code: known ? error.code : 'TRANSIENT_UNAVAILABLE', retryable: known ? error.retryable : true
  } };
}

module.exports = { API_VERSION, SCHEMA_VERSION, MAX_PAYLOAD_BYTES, INT_MAX, COLLECTIONS,
  ProtocolError, requireThat, object, identifier, integer, finite, validatePayload, storageKey,
  schema, reportInput, sameReport, compareBest, compareScores, validateScore, errorResponse };
