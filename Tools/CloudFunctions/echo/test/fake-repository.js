'use strict';

const { COLLECTIONS, compareScores } = require('../protocol');
const { boundedTransaction } = require('../repository');

const clone = (value) => value == null ? value : structuredClone(value);

// Optimistic transactions with read-version validation and private pending writes.
// Concurrent tests cross an actual barrier, then compete at commit, not a serial Map.
class FakeRepository {
  constructor() {
    this.documents = new Map();
    this.versions = new Map();
    this.attempts = 0;
    this.conflicts = 0;
    this.rollbacks = 0;
    this.forcedConflicts = 0;
    this.failOnSet = null;
    this.barrier = null;
  }

  key(collection, id) { return collection + '/' + id; }

  async get(collection, id) {
    return clone(this.documents.get(this.key(collection, id)) || null);
  }

  put(collection, id, document) {
    const key = this.key(collection, id);
    this.documents.set(key, { ...clone(document), _id: id });
    this.versions.set(key, (this.versions.get(key) || 0) + 1);
  }

  all(collection) {
    return [...this.documents.entries()].filter(([key]) => key.startsWith(collection + '/'))
      .map(([, value]) => clone(value));
  }

  synchronizeNextCommits(count = 2) {
    let release;
    const promise = new Promise((resolve) => { release = resolve; });
    this.barrier = { remaining: count, promise, release };
  }

  async start() {
    this.attempts++;
    const reads = new Map();
    const writes = new Map();
    let finished = false;
    const store = this;
    return {
      async get(collection, id) {
        const key = store.key(collection, id);
        if (!reads.has(key)) reads.set(key, store.versions.get(key) || 0);
        return clone(writes.get(key) || store.documents.get(key) || null);
      },
      async set(collection, id, document) {
        if (store.failOnSet === collection) {
          store.failOnSet = null;
          throw new Error('Injected storage failure');
        }
        const key = store.key(collection, id);
        if (!reads.has(key)) reads.set(key, store.versions.get(key) || 0);
        writes.set(key, { ...clone(document), _id: id });
      },
      async commit() {
        const barrier = store.barrier;
        if (barrier && barrier.remaining > 0) {
          barrier.remaining--;
          if (barrier.remaining === 0) { store.barrier = null; barrier.release(); }
          await barrier.promise;
        }
        let conflict = store.forcedConflicts > 0;
        if (conflict) store.forcedConflicts--;
        for (const [key, version] of reads) if ((store.versions.get(key) || 0) !== version) conflict = true;
        if (conflict) {
          store.conflicts++;
          throw { code: 'DATABASE_TRANSACTION_CONFLICT' };
        }
        for (const [key, value] of writes) {
          store.documents.set(key, value);
          store.versions.set(key, (store.versions.get(key) || 0) + 1);
        }
        finished = true;
        return {};
      },
      async rollback() {
        if (!finished) store.rollbacks++;
        writes.clear();
        finished = true;
        return {};
      }
    };
  }

  transaction(work) { return boundedTransaction(() => this.start(), work); }

  async leaderboard(boardId, limit) {
    return this.all(COLLECTIONS.scores).filter((score) => score.boardId === boardId)
      .sort(compareScores).slice(0, limit);
  }
}

module.exports = { FakeRepository };
