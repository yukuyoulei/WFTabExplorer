const wsService = require('../../utils/ws');

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

Page({
  data: {
    email: '',
    isEmailValid: false,
    sending: false,
    statusMessage: '',
    statusType: ''
  },

  onLoad() {
    this.socketHandlers = {
      open: this.handleSocketOpen.bind(this),
      close: this.handleSocketClose.bind(this),
      error: this.handleSocketError.bind(this),
      message: this.handleSocketMessage.bind(this)
    };

    Object.keys(this.socketHandlers).forEach((event) => {
      wsService.on(event, this.socketHandlers[event]);
    });

    wsService.connect();
  },

  onUnload() {
    if (this.socketHandlers) {
      Object.keys(this.socketHandlers).forEach((event) => {
        wsService.off(event, this.socketHandlers[event]);
      });
    }
    wsService.close();
  },

  onEmailInput(event) {
    const email = event.detail.value.trim();
    const isEmailValid = EMAIL_REGEX.test(email);
    this.setData({
      email,
      isEmailValid
    });
  },

  onRequestCode() {
    if (!this.data.isEmailValid || this.data.sending) {
      if (!this.data.isEmailValid) {
        wx.showToast({
          title: '请输入正确的邮箱',
          icon: 'none'
        });
      }
      return;
    }

    this.setData({
      sending: true,
      statusMessage: '',
      statusType: ''
    });

    wsService.send({
      type: 'RequestCode',
      email: this.data.email
    });
  },

  handleSocketOpen() {
    wx.showToast({
      title: '连接已建立',
      icon: 'success',
      duration: 800
    });
  },

  handleSocketClose() {
    if (this.data.sending) {
      this.setData({ sending: false });
    }
    wx.showToast({
      title: '连接已关闭',
      icon: 'none'
    });
  },

  handleSocketError() {
    if (this.data.sending) {
      this.setData({ sending: false });
    }
    wx.showToast({
      title: '连接出错',
      icon: 'none'
    });
  },

  handleSocketMessage(event) {
    let payload;
    try {
      payload = JSON.parse(event.data || '{}');
    } catch (error) {
      console.error('解析消息失败', error);
      wx.showToast({
        title: '响应格式错误',
        icon: 'none'
      });
      this.setData({ sending: false });
      return;
    }

    if (payload.type !== 'CodeSent') {
      wx.showToast({
        title: '未知响应',
        icon: 'none'
      });
      this.setData({ sending: false });
      return;
    }

    const success = !!payload.success;
    const message = payload.message || (success ? '验证码已发送（模拟）' : '请求失败');

    this.setData({
      sending: false,
      statusMessage: message,
      statusType: success ? 'success' : 'error'
    });

    wx.showToast({
      title: success ? '请求成功' : '请求失败',
      icon: success ? 'success' : 'none'
    });
  }
});
