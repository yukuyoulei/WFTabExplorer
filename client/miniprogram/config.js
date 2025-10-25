let WS_URL = 'ws://localhost:6000/ws';
let envVersion = 'develop';

try {
  const accountInfo = wx.getAccountInfoSync();
  if (accountInfo && accountInfo.miniProgram && accountInfo.miniProgram.envVersion) {
    envVersion = accountInfo.miniProgram.envVersion;
  }
} catch (error) {
  console.warn('获取小程序环境失败，已回退至开发地址', error);
}

if (envVersion === 'trial' || envVersion === 'release') {
  WS_URL = 'wss://kccoding.top/wechat/ws';
}

module.exports = {
  WS_URL
};
