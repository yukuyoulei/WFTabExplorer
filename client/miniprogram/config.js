const DEFAULT_WS_URL = 'ws://localhost:6000/ws';
const PROD_WS_URL = 'wss://kccoding.top/wechat/ws';

let envVersion = 'develop';

try {
  const accountInfo = wx.getAccountInfoSync();
  if (accountInfo && accountInfo.miniProgram && accountInfo.miniProgram.envVersion) {
    envVersion = accountInfo.miniProgram.envVersion;
  }
} catch (error) {
  console.warn('获取小程序环境失败，已回退至开发地址', error);
}

function getEnvVersion() {
  return envVersion;
}

function getWSUrl() {
  if (envVersion === 'trial' || envVersion === 'release') {
    return PROD_WS_URL;
  }
  return DEFAULT_WS_URL;
}

module.exports = {
  getEnvVersion,
  getWSUrl
};
