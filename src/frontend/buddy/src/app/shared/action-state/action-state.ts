import { Signal, computed, signal } from '@angular/core';

// The state of a mutation a component can run against one of several targets (a row, a date, a
// provider): idle, busy with one target, or failed for one target with a message to show. Replaces
// a busy-id signal plus a paired error signal, which could disagree. message is an i18n key, or an
// API error message the translate pipe passes through unchanged.
export type ActionState<Id> =
  { status: 'idle' } | { status: 'busy'; id: Id } | { status: 'error'; id: Id; message: string };

export interface Action<Id> {
  readonly state: Signal<ActionState<Id>>;
  // True while any target is busy.
  readonly busy: Signal<boolean>;
  isBusy(id: Id): boolean;
  // Runs work for id: busy while it runs, idle when it succeeds, failed with errorMessage (or what
  // it returns for the thrown error) when it throws. Resolves to whether it succeeded. If another
  // run started meanwhile, a success leaves that run's state alone, while a failure still takes
  // over, so a failure is never lost to a later success.
  run(
    id: Id,
    work: () => Promise<void>,
    errorMessage: string | ((error: unknown) => string),
  ): Promise<boolean>;
  // Fails id without running anything (a client-side check that rejects the input).
  fail(id: Id, message: string): void;
  reset(): void;
  // Back to idle only when showing an error, so a run still in flight stays busy.
  clearError(): void;
}

export function createAction<Id = true>(): Action<Id> {
  const state = signal<ActionState<Id>>({ status: 'idle' });

  return {
    state: state.asReadonly(),
    busy: computed(() => state().status === 'busy'),
    isBusy: (id) => {
      const current = state();
      return current.status === 'busy' && current.id === id;
    },
    run: async (id, work, errorMessage) => {
      const busy: ActionState<Id> = { status: 'busy', id };
      state.set(busy);

      try {
        await work();

        if (state() === busy) {
          state.set({ status: 'idle' });
        }

        return true;
      } catch (error: unknown) {
        const message = typeof errorMessage === 'string' ? errorMessage : errorMessage(error);
        state.set({ status: 'error', id, message });
        return false;
      }
    },
    fail: (id, message) => state.set({ status: 'error', id, message }),
    reset: () => state.set({ status: 'idle' }),
    clearError: () => {
      if (state().status === 'error') {
        state.set({ status: 'idle' });
      }
    },
  };
}
