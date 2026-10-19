'use strict';

const { createHandler } = require('./handler');
const { createWxRepository, sdkError, reportDiagnostic } = require('./repository');
const { errorResponse } = require('./protocol');

function complete(event, response) {
  if (!response.ok) {
    const action = event && ['publish', 'get', 'report', 'leaderboard'].includes(event.action)
      ? event.action : 'unknown';
    console.warn('echo', { action, code: response.error.code, retryable: response.error.retryable });
  }
  return response;
}

function createMain({ loadCloud = () => require('wx-server-sdk'), diagnostic = () => {} } = {}) {
  let cloud;
  let handle;
  return async (event) => {
    let stage = 'sdk.load';
    try {
      if (!handle) {
        cloud = loadCloud();
        stage = 'sdk.init';
        cloud.init({ env: cloud.DYNAMIC_CURRENT_ENV, timeout: 4000, retries: 0 });
        stage = 'database.init';
        const repository = createWxRepository(cloud.database({ throwOnNotFound: false }), { diagnostic });
        stage = 'handler.init';
        handle = createHandler({ repository });
      }
      // Capture the caller on every invocation. Never cache an OPENID or read event.openid.
      stage = 'context.read';
      const context = cloud.getWXContext();
      stage = 'handler';
      return complete(event, await handle(event, { OPENID: context.OPENID }));
    } catch (error) {
      reportDiagnostic(diagnostic, stage, error);
      return complete(event, errorResponse(sdkError(error)));
    }
  };
}

exports.createMain = createMain;
exports.main = createMain({ diagnostic: (entry) => console.warn('echo.diagnostic', entry) });
