import { useQuery } from '@tanstack/react-query';
import { healthApi } from '@/api/healthApi';

export function useHealthCheck() {
  return useQuery({
    queryKey: ['health'],
    queryFn: healthApi.check,
    staleTime: 30_000,
  });
}
