# Spec patterns (Vitest via `@angular/build:unit-test`)

Copy from the cited files rather than inventing new helpers. Imports come from `vitest` (`describe`, `it`, `expect`, `vi`, `beforeEach`, `afterEach`), not Jasmine.

## Component spec: stub the service layer

From `src/app/features/guardian/doses-today/doses-today.spec.ts`:

```ts
interface Stubs {
  guardians?: Partial<GuardiansService>;
  medicines?: Partial<MedicinesService>;
}

async function setup(stubs: Stubs = {}) {
  const guardiansStub: Partial<GuardiansService> = {
    listMyChildren: vi.fn(async () => [child()]),
    ...stubs.guardians
  };
  const medicinesStub: Partial<MedicinesService> = {
    listDoses: vi.fn(async () => []),
    setDoseStatus: vi.fn(),
    ...stubs.medicines
  };

  await TestBed.configureTestingModule({
    imports: [DosesToday],
    providers: [
      provideRouter([]),
      { provide: GuardiansService, useValue: guardiansStub },
      { provide: MedicinesService, useValue: medicinesStub }
    ]
  }).compileComponents();

  const fixture = TestBed.createComponent(DosesToday);
  return { fixture, guardians: guardiansStub, medicines: medicinesStub };
}
```

- Test-data factories with `overrides: Partial<T>` (`child()`, `dose()`, `occurrence()`).
- `TranslatePipe` / `TranslationService` are used **unstubbed**: jsdom's language is `en-US`, so the English strings render and are asserted on (`textContent` contains `'Danger zone'`).
- Pending states: a `deferred<T>()` helper (see `src/app/features/child/home/home.spec.ts`) to hold a promise open and assert the spinner, then resolve.

## `settle()` instead of `whenStable()`

The app is zoneless; a stubbed service's plain `Promise` isn't a `PendingTasks` entry, so `fixture.whenStable()` resolves immediately. Flush a macrotask (docs/testing.md):

```ts
async function settle(fixture: ComponentFixture<unknown>) {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}
```

Repeat the flush in a loop when handlers chain several awaits (`doses-today.spec.ts` loops 10 times). Re-read `fixture.nativeElement` after settling.

## Querying controls by role

`app-toggle` renders `<button role="switch" aria-checked>`, `app-segmented-control` renders `role="radiogroup"` / `role="radio"` buttons. Don't look for `input[type="checkbox"]` or `<select>` for these. From `src/app/features/guardian/calendar/agenda/agenda.spec.ts`:

```ts
function findToggle(root: ParentNode, ariaLabel?: string): HTMLButtonElement {
  const toggles = Array.from(root.querySelectorAll<HTMLButtonElement>('button[role="switch"]'));
  return (ariaLabel ? toggles.find((t) => t.getAttribute('aria-label') === ariaLabel) : toggles[0])!;
}
const toggleIsChecked = (t: HTMLButtonElement) => t.getAttribute('aria-checked') === 'true';

findToggle(compiled, 'Home').click();
await settle(fixture);
```

Find buttons by trimmed text (`findButton(compiled, 'Mark taken')`). Native `<select>`/`ngModel` writes need a settle too (see the note in `src/app/features/guardian/pickup/pickup-cell/pickup-cell.spec.ts`).

## Service spec: `HttpTestingController`

From `src/app/core/medicines.service.spec.ts`:

```ts
beforeEach(() => {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: RuntimeConfigService, useValue: { apiBaseUrl } as Partial<RuntimeConfigService> }
    ]
  });
  service = TestBed.inject(MedicinesService);
  httpMock = TestBed.inject(HttpTestingController);
});

afterEach(() => httpMock.verify());

it('POSTs the create request and returns the created schedule', async () => {
  const promise = service.createSchedule(childId, request);
  const req = httpMock.expectOne(`${base()}/schedules`);
  expect(req.request.method).toBe('POST');
  expect(req.request.body).toEqual(request);
  req.flush(created);
  await expect(promise).resolves.toEqual(created);
});
```

Assert the exact URL, method, body and query params (mutation testing will flag anything looser). The retry/`Idempotency-Key` behaviour itself is covered once in `src/app/core/http-idempotency.spec.ts`; don't re-test it per service.

## `matchMedia`

`src/test-setup.ts` (wired via `angular.json` `test.options.setupFiles`) stubs `window.matchMedia` globally so anything injecting `ThemeService` works. A spec that cares about the OS preference stubs it itself (`src/app/core/theme.service.spec.ts`).
