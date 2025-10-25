const { WS_URL } = require('../config');

class WebSocketService {
  constructor() {
    this.socketTask = null;
    this.connected = false;
    this.manualClose = false;
    this.reconnectTimer = null;
    this.messageQueue = [];
    this.listeners = {
      open: [],
      close: [],
      error: [],
      message: []
    };
  }

  connect() {
    if (this.socketTask) {
      return;
    }

    this.manualClose = false;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    const socketTask = wx.connectSocket({
      url: WS_URL
    });

    this.socketTask = socketTask;

    socketTask.onOpen((event) => {
      this.connected = true;
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
      this.connected = false;
      this.socketTask = null;
      this.emit('close', event);
      if (!this.manualClose) {
        this.scheduleReconnect();
      }
    });
  }

  scheduleReconnect() {
    if (this.reconnectTimer) {
      return;
    }

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.connect();
    }, 3000);
  }

  flushQueue() {
    if (!this.connected || !this.socketTask) {
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

    if (this.connected && this.socketTask) {
      this.socketTask.send({
        data,
        fail: (error) => {
          this.emit('error', error || { errMsg: 'sendSocketMessage failed' });
        }
      });
    } else {
      this.messageQueue.push(data);
      this.connect();
    }
  }

  close() {
    this.manualClose = true;
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    if (this.socketTask) {
      this.socketTask.close({
        code: 1000,
        reason: '手动关闭连接'
      });
      this.socketTask = null;
    }

    this.connected = false;
  }

  on(event, handler) {
    const list = this.listeners[event];
    if (!list) {
      return;
    }

    if (typeof handler === 'function' && !list.includes(handler)) {
      list.push(handler);
    }
  }

  off(event, handler) {
    const list = this.listeners[event];
    if (!list) {
      return;
    }

    if (!handler) {
      this.listeners[event] = [];
      return;
    }

    const index = list.indexOf(handler);
    if (index !== -1) {
      list.splice(index, 1);
    }
  }

  emit(event, payload) {
    const list = this.listeners[event];
    if (!list) {
      return;
    }

    list.forEach((handler) => {
      try {
        handler(payload);
      } catch (error) {
        console.error('事件处理异常', event, error);
      }
    });
  }
}

module.exports = new WebSocketService();
