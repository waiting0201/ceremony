import { version } from '../../package.json';

export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5050/api/v1',
  version: `v${version}`,
};
