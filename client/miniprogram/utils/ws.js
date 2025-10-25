const { getWSUrl } = require('../config');

const DEFAULT_MAX_RETRIES = 5;
const RECONNECT_DELAY = 1500;
const LISTEN_EVENTS = ['open', 'close', 'error', 'message', 'reconnecting'];

class WebSocketManager {
  constructor() {
    this.socketTask = null;
    this.socketOpen = false;
    this.manualClose = false;
    this.connecting = false;
    this.reconnecting = false;
    this.retryCount = 0;
    this.maxRetries = DEFAULT_MAX_RETRIES;
    this.reconnectTimer = null;
    this.messageQueue = [];
    this.listeners = this.createListenerBuckets();
  }

  createListenerBuckets() {
    return LISTEN_EVENTS.reduce((acc, event) => {
      acc[event] = new Set();
      return acc;
    }, {});
  }

  normalizeMaxRetries(value) {
    if (value === undefined) {
      return DEFAULT_MAX_RETRIES;
    }

    const parsed = Number(value);
    if (Number.isInteger(parsed) && parsed >= 0) {
      return parsed;
    }

    return DEFAULT_MAX_RETRIES;
  }

  connect(options = {}) {
    this.maxRetries = this.normalizeMaxRetries(options.maxRetries);
    this.manualClose = false;

    if (this.socketTask || this.connecting || this.socketOpen) {
      return;
    }

    this.retryCount = 0;
    this.clearReconnectTimer();
    this.createSocket();
  }

  createSocket({ reconnecting = false } = {}) {
    this.connecting = true;
    this.reconnecting = reconnecting;

    let socketTask;
    try {
      socketTask = wx.connectSocket({
        url: getWSUrl()
      });
    } catch (error) {
      this.connecting = false;
      this.socketTask = null;
      this.emit('error', error || { errMsg: 'connectSocket failed' });
      this.scheduleReconnect();
      return;
    }

    this.socketTask = socketTask;

    socketTask.onOpen((event) => {
      this.connecting = false;
      this.socketOpen = true;
      this.reconnecting = false;
      this.retryCount = 0;
      this.emit('open', event);
      this.flushQueue();
    });

    socketTask.onMessage((event) => {
      this.emit('message', event);
    });

    socketTask.onError((event) => {
      this.emit('error', event);
    });

    socketTask.onClose((event) => {
      this.socketOpen = false;
      this.socketTask = null;
      this.connecting = false;
      this.emit('close', event);

      if (!this.manualClose) {
        this.scheduleReconnect();
      }
    });
  }

  scheduleReconnect() {
    if (this.manualClose || this.maxRetries === 0) {
      this.connecting = false;
      this.reconnecting = false;
      return;
    }

    if (this.retryCount >= this.maxRetries) {
      this.connecting = false;
      this.reconnecting = false;
      this.emit('error', {
        errMsg: '重连次数已用尽'
      });
      return;
    }

    if (this.reconnectTimer) {
      return;
    }

    this.retryCount += 1;
    this.connecting = true;
    this.reconnecting = true;

    this.emit('reconnecting', {
      attempt: this.retryCount,
      maxRetries: this.maxRetries
    });

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.createSocket({ reconnecting: true });
    }, RECONNECT_DELAY);
  }

  clearReconnectTimer() {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  flushQueue() {
    if (!this.socketOpen || !this.socketTask) {
      return;
    }

    while (this.messageQueue.length > 0) {
      const data = this.messageQueue.shift();
      this.socketTask.send({
        data,
        fail: (error) => {
          this.emit('error', error || { errMsg: 'sendSocketMessage failed' });
          this.messageQueue.unshift(data);
          if (this.socketTask) {
            this.socketTask.close({
              code: 1001,
              reason: '发送失败，准备重连'
            });
          }
        }
      });
    }
  }

  send(payload) {
    const data = typeof payload === 'string' ? payload : JSON.stringify(payload);

    if (this.socketOpen && this.socketTask) {
      this.socketTask.send({
        data,
        fail: (error) => {
          this.emit('error', error || { errMsg: 'sendSocketMessage failed' });
        }
      });
      return;
    }

    if (!this.manualClose) {
      this.messageQueue.push(data);
      if (!this.socketTask && !this.connecting) {
        this.scheduleReconnect();
      }
    }
  }

  close() {
    this.manualClose = true;
    this.connecting = false;
    this.reconnecting = false;
    this.clearReconnectTimer();
    this.messageQueue = [];

    if (this.socketTask) {
      this.socketTask.close({
        code: 1000,
        reason: '手动关闭连接'
      });
      this.socketTask = null;
    }

    this.socketOpen = false;
  }

  on(event, handler) {
    this.addListener(event, handler);
  }

  off(event, handler) {
    this.removeListener(event, handler);
  }

  onOpen(handler) {
    this.addListener('open', handler);
  }

  offOpen(handler) {
    this.removeListener('open', handler);
  }

  onClose(handler) {
    this.addListener('close', handler);
  }

  offClose(handler) {
    this.removeListener('close', handler);
  }

  onError(handler) {
    this.addListener('error', handler);
  }

  offError(handler) {
    this.removeListener('error', handler);
  }

  onMessage(handler) {
    this.addListener('message', handler);
  }

  offMessage(handler) {
    this.removeListener('message', handler);
  }

  onReconnecting(handler) {
    this.addListener('reconnecting', handler);
  }

  offReconnecting(handler) {
    this.removeListener('reconnecting', handler);
  }

  addListener(event, handler) {
    const bucket = this.listeners[event];
    if (!bucket || typeof handler !== 'function') {
      return;
    }

    bucket.add(handler);
  }

  removeListener(event, handler) {
    const bucket = this.listeners[event];
    if (!bucket) {
      return;
    }

    if (!handler) {
      bucket.clear();
      return;
    }

    bucket.delete(handler);
  }

  emit(event, payload) {
    const bucket = this.listeners[event];
    if (!bucket) {
      return;
    }

    bucket.forEach((handler) => {
      try {
        handler(payload);
      } catch (error) {
        console.error('事件处理异常', event, error);
      }
    });
  }

  isOpen() {
    return this.socketOpen;
  }

  isConnecting() {
    return this.connecting;
  }

  isReconnecting() {
    return this.reconnecting;
  }
}

module.exports = new WebSocketManager();
