'use strict';

const { COLLECTIONS, ProtocolError } = require('./protocol');

const DIAGNOSTIC_STAGES = new Set(['sdk.load', 'sdk.init', 'database.init', 'context.read',
  'handler.init', 'handler', 'database.get', 'database.leaderboard', 'transaction.start', 'transaction.work',
  'transaction.get', 'transaction.set', 'transaction.commit', 'transaction.rollback', 'transaction.budget']);
const ERROR_NAMES = new Set(['Error', 'TypeError', 'ReferenceError', 'RangeError', 'SyntaxError',
  'CloudSDKError', 'TencentCloudSDKHttpException', 'AbortError']);

function diagnosticCode(value) {
  if (Number.isSafeInteger(value) && Math.abs(value) <= 2147483647) return value;
  return typeof value === 'string' && /^(?:[A-Z][A-Z0-9_]{0,63}|-?\d{1,10})$/.test(value)
    ? value : 'unknown';
}

// Never pass an SDK error object through to a log sink: it can contain request data.
function reportDiagnostic(diagnostic, stage, error) {
  try {
    const code = diagnosticCode(error && error.code);
    const errCode = diagnosticCode(error && error.errCode);
    const name = error && error.name;
    const errorName = ERROR_NAMES.has(name) ? name : 'unknown';
    const raw = error && (error.errMsg || error.message);
    const description = [code, errCode, typeof raw === 'string' ? raw.slice(0, 512) : ''].join(' ');
    let reason = 'unknown';
    if (stage === 'transaction.budget' || /timeout|timed out|ETIMEDOUT/i.test(description)) reason = 'timeout';
    else if (code === 'MODULE_NOT_FOUND' || code === 'ERR_MODULE_NOT_FOUND'
      || /cannot find module|module not found/i.test(description)) reason = 'missing_module';
    else if (/permission denied|not authorized|unauthorized|access denied/i.test(description)) reason = 'permission';
    else if (/collection.*(?:not exist|does not exist)|environment.*(?:not exist|invalid)|index.*(?:not exist|required)|未取到.*env|环境.*(?:不存在|无效|未开通)/i.test(description)) reason = 'configuration';
    else if (/invalid[_ ](?:argument|param)|parameter.*(?:invalid|required)|参数.*(?:无效|错误|必需)/i.test(description)) reason = 'invalid_argument';
    else if (isConflict(error)) reason = 'transaction_conflict';
    else if (/ECONNRESET|ECONNREFUSED|ENOTFOUND|network|socket/i.test(description)) reason = 'network';
    else if (['TypeError', 'ReferenceError', 'RangeError', 'SyntaxError'].includes(errorName)) reason = 'runtime';
    diagnostic({ stage: DIAGNOSTIC_STAGES.has(stage) ? stage : 'unknown', code, errCode, errorName, reason });
  } catch { /* Diagnostics must never change the response or retry behavior. */ }
}

function checked(response) {
  if (response && ((response.code && response.code !== 0)
    || (response.errCode && response.errCode !== 0))) throw response;
  return response;
}

function isConflict(error) {
  return error && (error.code === 'DATABASE_TRANSACTION_CONFLICT'
    || error.errCode === 'DATABASE_TRANSACTION_CONFLICT'
    || /(?:TransactionConflict|transaction conflict)/i.test(error.errMsg || error.message || ''));
}

function sdkError(error) {
  if (error instanceof ProtocolError) return error;
  const description = String(error && (error.errMsg || error.message || error.code) || '');
  if (/permission denied|not authorized|unauthorized|access denied/i.test(description)) {
    return new ProtocolError('PERMISSION_DENIED');
  }
  if (/collection.*(?:not exist|does not exist)|environment.*(?:not exist|invalid)|index.*(?:not exist|required)/i.test(description)) {
    return new ProtocolError('CLOUD_NOT_CONFIGURED');
  }
  return new ProtocolError('TRANSIENT_UNAVAILABLE', true);
}

// No SDK runTransaction helper: it would add another retry loop.
// Ambiguous commit failures are not retried here. The client reuses its business key.
async function boundedTransaction(start, work, { clock = Date.now, budgetMs = 4000,
  diagnostic = () => {} } = {}) {
  const deadline = clock() + budgetMs;
  for (let attempt = 0; attempt < 2; attempt++) {
    let transaction;
    let stage = 'transaction.budget';
    try {
      if (clock() >= deadline) throw new ProtocolError('TRANSIENT_UNAVAILABLE', true);
      stage = 'transaction.start';
      transaction = checked(await start());
      stage = 'transaction.work';
      const value = await work(transaction);
      stage = 'transaction.budget';
      if (clock() >= deadline) throw new ProtocolError('TRANSIENT_UNAVAILABLE', true);
      stage = 'transaction.commit';
      checked(await transaction.commit());
      return value;
    } catch (error) {
      if (!(error instanceof ProtocolError) || stage === 'transaction.budget') reportDiagnostic(diagnostic, stage, error);
      if (transaction) {
        try { checked(await transaction.rollback()); } catch (rollbackError) {
          reportDiagnostic(diagnostic, 'transaction.rollback', rollbackError);
        }
      }
      if (attempt === 0 && isConflict(error) && clock() < deadline) continue;
      throw sdkError(error);
    }
  }
  throw new ProtocolError('TRANSIENT_UNAVAILABLE', true);
}

function createWxRepository(database, options = {}) {
  const diagnostic = options.diagnostic || (() => {});
  async function read(source, collection, key) {
    try {
      const response = checked(await source.collection(collection).doc(key).get());
      const data = response && response.data;
      return Array.isArray(data) ? (data[0] || null) : (data || null);
    } catch (error) {
      // A missing collection is configuration failure, not a missing document.
      const description = String(error && (error.errMsg || error.message) || '');
      if (/document(?: with _id \S+)?\s+(?:does not exist|not exists|not exist)/i.test(description)) return null;
      throw error;
    }
  }

  return {
    async get(collection, key) {
      try { return await read(database, collection, key); } catch (error) {
        reportDiagnostic(diagnostic, 'database.get', error);
        throw sdkError(error);
      }
    },
    transaction(work) {
      return boundedTransaction(() => database.startTransaction(), (transaction) => work({
        get: async (collection, key) => {
          try { return await read(transaction, collection, key); } catch (error) {
            reportDiagnostic(diagnostic, 'transaction.get', error);
            throw error;
          }
        },
        set: async (collection, key, document) => {
          try { return checked(await transaction.collection(collection).doc(key).set({ data: document })); }
          catch (error) {
            reportDiagnostic(diagnostic, 'transaction.set', error);
            throw error;
          }
        }
      }), options);
    },
    async leaderboard(boardId, limit) {
      try {
        const response = checked(await database.collection(COLLECTIONS.scores)
          .where({ boardId })
          .orderBy('distanceMeters', 'desc').orderBy('playerLeadMeters', 'desc')
          .orderBy('achievedAt', 'asc').orderBy('_id', 'asc')
          .field({ _id: true, schemaVersion: true, boardId: true, challengerOpenid: true,
            bestChallengeId: true, endReason: true, distanceMeters: true,
            playerLeadMeters: true, playerWon: true, achievedAt: true })
          .limit(limit).get());
        if (!response || !Array.isArray(response.data)) throw new ProtocolError('TRANSIENT_UNAVAILABLE', true);
        return response.data;
      } catch (error) {
        reportDiagnostic(diagnostic, 'database.leaderboard', error);
        throw sdkError(error);
      }
    }
  };
}

module.exports = { createWxRepository, boundedTransaction, checked, isConflict, sdkError, reportDiagnostic };
