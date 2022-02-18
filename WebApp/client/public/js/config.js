export async function getServerConfig() {
  const protocolEndPoint = location.origin + '/config';
  const createResponse = await fetch(protocolEndPoint);
  return await createResponse.json();
}

export function getRTCConfiguration() {
  let config = {};
  config.sdpSemantics = 'unified-plan';
  config.iceServers = [{
      urls: ['turn:52.131.241.185:3478?transport=tcp'], 
      username: 'username', 
      credential: 'password'
    }
  ];

  return config;
}
