import { useEffect, useState } from 'react';

/** Delays reflecting `value` until it's stopped changing for `delayMs` — used to avoid firing
 * a network request on every keystroke (global search, and any future search-as-you-type UI). */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timeout);
  }, [value, delayMs]);

  return debounced;
}
