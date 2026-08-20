import type { UserSummary } from './project';

export interface TaskActivity {
  id: string;
  /** Null if the acting user was later deleted — the event itself is kept. */
  user: UserSummary | null;
  action: string;
  fieldName: string | null;
  oldValue: string | null;
  newValue: string | null;
  createdAt: string;
}
