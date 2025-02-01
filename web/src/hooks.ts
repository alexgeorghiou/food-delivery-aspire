import { useEffect, useRef, useState } from 'react';

export interface AsyncState<T> {
  data: T | null;
  error: Error | null;
  loading: boolean;
}

/** Runs an async loader whenever `deps` change, ignoring results from stale calls. */
export function useAsync<T>(loader: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [state, setState] = useState<AsyncState<T>>({ data: null, error: null, loading: true });

  useEffect(() => {
    let cancelled = false;
    setState((s) => ({ ...s, loading: true, error: null }));
    loader()
      .then((data) => !cancelled && setState({ data, error: null, loading: false }))
      .catch((error: Error) => !cancelled && setState({ data: null, error, loading: false }));
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return state;
}

/**
 * Polls `loader` every `intervalMs` until `shouldStop` returns true for the latest value.
 * Polling pauses while the tab is hidden to avoid hammering the API from background tabs.
 */
export function usePolling<T>(
  loader: () => Promise<T>,
  intervalMs: number,
  shouldStop: (value: T) => boolean,
  deps: unknown[],
) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [tick, setTick] = useState(0);
  const stopRef = useRef(shouldStop);
  stopRef.current = shouldStop;

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const run = async () => {
      if (document.visibilityState === 'visible') {
        try {
          const value = await loader();
          if (cancelled) return;
          setData(value);
          setError(null);
          if (stopRef.current(value)) return;
        } catch (e) {
          if (cancelled) return;
          setError(e as Error);
        }
      }
      timer = setTimeout(run, intervalMs);
    };

    void run();
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick, intervalMs]);

  return { data, error, refresh: () => setTick((t) => t + 1) };
}
