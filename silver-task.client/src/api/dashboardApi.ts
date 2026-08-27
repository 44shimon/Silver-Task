import { httpClient } from './httpClient';
import type { ActivityFeedItem, DashboardData, StatsRange, TeamWorkload, UpcomingRange, WorkflowSummary } from '@/types/dashboard';

/** Every endpoint resolves the caller from the auth cookie server-side — there is no user id
 * parameter here to get wrong (see DashboardController's own doc comment). */
export const dashboardApi = {
  get: (upcomingRange: UpcomingRange, statsRange: StatsRange) =>
    httpClient.get<DashboardData>(`/dashboard?upcomingRange=${upcomingRange}&statsRange=${statsRange}`),
  /** Undefined (204 No Content) when the caller doesn't manage any project. */
  teamWorkload: () => httpClient.get<TeamWorkload | undefined>('/dashboard/team-workload'),
  activity: (mineOnly: boolean, limit = 15) =>
    httpClient.get<ActivityFeedItem[]>(`/dashboard/activity?mineOnly=${mineOnly}&limit=${limit}`),
  workflow: () => httpClient.get<WorkflowSummary>('/dashboard/workflow'),
};
