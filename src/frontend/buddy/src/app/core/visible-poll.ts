// Calls refresh every intervalMs, and again whenever the page becomes visible, as the transition
// warning banner does: Buddy has no push channel, so a child's device notices a guardian's change
// within a minute (docs/backend/analysis/first-then-board.md, Question 6). Returns the stop
// function for ngOnDestroy.
export function pollWhileOpen(refresh: () => void, intervalMs: number): () => void {
  const intervalId = setInterval(refresh, intervalMs);
  const onVisibilityChange = (): void => {
    if (document.visibilityState === 'visible') {
      refresh();
    }
  };

  document.addEventListener('visibilitychange', onVisibilityChange);

  return () => {
    clearInterval(intervalId);
    document.removeEventListener('visibilitychange', onVisibilityChange);
  };
}
