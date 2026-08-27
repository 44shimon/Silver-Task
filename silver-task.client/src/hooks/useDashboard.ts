import { useQuery } from '@tanstack/react-query';
import { dashboardApi } from '@/api/dashboardApi';
import type { StatsRange, UpcomingRange } from '@/types/dashboard';

export function useDashboard(upcomingRange: UpcomingRange, statsRange: StatsRange) {
  return useQuery({
    queryKey: ['dashboard', upcomingRange, statsRange],
    queryFn: () => dashboardApi.get(upcomingRange, statsRange),
  });
}

export function useTeamWorkload() {
  return useQuery({
    queryKey: ['dashboard', 'team-workload'],
    queryFn: dashboardApi.teamWorkload,
  });
}

export function useRecentActivity(mineOnly: boolean, limit = 15) {
  return useQuery({
    queryKey: ['dashboard', 'activity', mineOnly, limit],
    queryFn: () => dashboardApi.activity(mineOnly, limit),
  });
}
