Plan: Add data-driven list component (Angular v22, signals-first, TailwindCSS)

Steps:
1. Add a standalone `ItemsListComponent` that uses signals for local UI state.
2. Add `ItemsService` using `HttpClient` and expose `items$` as an Observable.
3. In the component, convert the Observable to a signal (`toSignal`) for template consumption.
4. Provide a minimal Jasmine/Karma unit test for the component.
5. Include `tailwind-integration.md` with quick install and integration notes.

Assumptions:
- Angular v22 provides `signal` and `toSignal` interop helpers.
- Project uses `HttpClientModule` and standard Angular testing setup.
- Tailwind will be integrated into global styles as shown in the notes.

Deliverables:
- `items.service.ts`
- `items-list.component.ts` and `items-list.component.html`
- `items-list.component.spec.ts`
- `tailwind-integration.md`
