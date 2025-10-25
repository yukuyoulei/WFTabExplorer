const wsService = require('../../utils/ws');

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const STATUS = {
  CONNECTING: { text: '连接中...', level: 'info' },
  CONNECTED: { text: '已连接', level: 'success' },
  DISCONNECTED: { text: '未连接', level: 'warning' }
};

Page({
  data: {
    email: '',
    emailValid: false,
    connecting: false,
    socketOpen: false,
    loading: false,
    lastError: '',
    statusText: STATUS.DISCONNECTED.text,
    statusLevel: STATUS.DISCONNECTED.level
  },

  onLoad() {
    this.registerSocketEvents();
    this.bootstrapSocket();
  },

  onUnload() {
    this.unregisterSocketEvents();
    wsService.close();
  },

  registerSocketEvents() {
    if (this.socketHandlers) {
      return;
    }

    this.socketHandlers = {
      open: this.handleSocketOpen.bind(this),
      close: this.handleSocketClose.bind(this),
      error: this.handleSocketError.bind(this),
      message: this.handleSocketMessage.bind(this),
      reconnecting: this.handleSocketReconnecting.bind(this)
    };

    wsService.onOpen(this.socketHandlers.open);
    wsService.onClose(this.socketHandlers.close);
    wsService.onError(this.socketHandlers.error);
    wsService.onMessage(this.socketHandlers.message);
    wsService.onReconnecting(this.socketHandlers.reconnecting);
  },

  unregisterSocketEvents() {
    if (!this.socketHandlers) {
      return;
    }

    wsService.offOpen(this.socketHandlers.open);
    wsService.offClose(this.socketHandlers.close);
    wsService.offError(this.socketHandlers.error);
    wsService.offMessage(this.socketHandlers.message);
    wsService.offReconnecting(this.socketHandlers.reconnecting);

    this.socketHandlers = null;
  },

  bootstrapSocket() {
    const socketOpen = wsService.isOpen();
    const connecting = wsService.isConnecting();
    const willConnect = !socketOpen;
    const connectingState = connecting || willConnect;

    let status = STATUS.DISCONNECTED;
    if (socketOpen) {
      status = STATUS.CONNECTED;
    } else if (connectingState) {
      status = STATUS.CONNECTING;
    }

    this.setData({
      socketOpen,
      connecting: connectingState,
      lastError: '',
      statusText: status.text,
      statusLevel: status.level
    });

    if (willConnect) {
      wsService.connect({ maxRetries: 5 });
    }
  },

  validateEmail(email) {
    return EMAIL_REGEX.test(email);
  },

  onEmailInput(event) {
    const email = (event.detail.value || '').trim();

    this.setData({
      email,
      emailValid: this.validateEmail(email)
    });
  },

  onGetCodeTap() {
    if (!this.data.emailValid) {
      wx.showToast({
        title: '请输入正确的邮箱',
        icon: 'none'
      });
      return;
    }

    if (!this.data.socketOpen) {
      wx.showToast({
        title: '连接尚未建立',
        icon: 'none'
      });
      return;
    }

    if (this.data.loading) {
      return;
    }

    this.setData({
      loading: true,
      lastError: '',
      statusText: '发送中...',
      statusLevel: 'info'
    });

    wsService.send({
      type: 'RequestCode',
      email: this.data.email
    });
  },

  handleSocketOpen() {
    this.setData({
      connecting: false,
      socketOpen: true,
      lastError: '',
      statusText: STATUS.CONNECTED.text,
      statusLevel: STATUS.CONNECTED.level
    });

    wx.showToast({
      title: '连接成功',
      icon: 'success',
      duration: 800
    });
  },

  handleSocketClose() {
    const reconnecting = wsService.isReconnecting();

    if (reconnecting) {
      this.setData({
        socketOpen: false,
        connecting: true,
        loading: false,
        lastError: '',
        statusText: STATUS.CONNECTING.text,
        statusLevel: STATUS.CONNECTING.level
      });
      return;
    }

    this.setData({
      socketOpen: false,
      connecting: false,
      loading: false,
      lastError: '连接已断开',
      statusText: STATUS.DISCONNECTED.text,
      statusLevel: STATUS.DISCONNECTED.level
    });
  },

  handleSocketError(event = {}) {
    const message = event.errMsg || '网络异常';

    this.setData({
      lastError: message,
      socketOpen: wsService.isOpen(),
      connecting: wsService.isConnecting(),
      statusText: message,
      statusLevel: 'error',
      loading: false
    });
  },

  handleSocketReconnecting({ attempt, maxRetries } = {}) {
    let statusText = STATUS.CONNECTING.text;
    if (typeof attempt === 'number' && typeof maxRetries === 'number') {
      statusText = `连接中（${attempt}/${maxRetries}）`;
    }

    this.setData({
      connecting: true,
      socketOpen: false,
      statusText,
      statusLevel: STATUS.CONNECTING.level,
      lastError: ''
    });
  },

  handleSocketMessage(event) {
    let payload = event && event.data;

    if (typeof payload === 'string') {
      try {
        payload = JSON.parse(payload);
      } catch (error) {
        console.error('解析消息失败', error);
        this.setData({
          loading: false,
          lastError: '响应格式异常',
          statusText: '响应格式异常',
          statusLevel: 'error'
        });
        wx.showToast({
          title: '响应格式错误',
          icon: 'none'
        });
        return;
      }
    }

    if (!payload || payload.type !== 'CodeSent') {
      if (this.data.loading) {
        this.setData({
          loading: false,
          statusText: STATUS.CONNECTED.text,
          statusLevel: STATUS.CONNECTED.level
        });
      }
      return;
    }

    const success = !!payload.success;
    const message = payload.message || (success ? '验证码已发送' : '验证码发送失败');

    if (success) {
      this.setData({
        loading: false,
        lastError: '',
        statusText: STATUS.CONNECTED.text,
        statusLevel: STATUS.CONNECTED.level
      });
    } else {
      this.setData({
        loading: false,
        lastError: message,
        statusText: message,
        statusLevel: 'error'
      });
    }

    wx.showToast({
      title: message,
      icon: success ? 'success' : 'none'
    });
  }
});
