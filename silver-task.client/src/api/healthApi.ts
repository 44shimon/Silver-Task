import { httpClient } from './httpClient';

export interface HealthStatus {
  status: string;
  timeUtc: string;
}

export const healthApi = {
  check: () => httpClient.get<HealthStatus>('/health'),
};
