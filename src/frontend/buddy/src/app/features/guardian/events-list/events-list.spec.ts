import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { UserEventItem, UserEventsPage, UserEventsService } from '../../../core/user-events.service';
import { EventsList } from './events-list';

describe('EventsList', () => {
  let httpMock: HttpTestingController;

  function page(overrides: Partial<UserEventsPage> = {}): UserEventsPage {
    return { items: [], previousCursor: null, nextCursor: null, ...overrides };
  }

  async function setup(listCurrentUserEvents: UserEventsService['listCurrentUserEvents'] = vi.fn(async () => page())) {
    const userEventsStub: Partial<UserEventsService> = { listCurrentUserEvents };

    await TestBed.configureTestingModule({
      imports: [EventsList],
      providers: [
        // The rendered event-type child components (e.g. NameUpdatedEvent) use UserDatePipe,
        // which injects the real UsersService -- and that needs HttpClient to be constructible,
        // even though nothing in these tests ever triggers a request through it.
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: UserEventsService, useValue: userEventsStub }
      ]
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(EventsList);
    return { fixture, listCurrentUserEvents };
  }

  afterEach(() => {
    httpMock.verify();
  });

  // The service is stubbed directly rather than via HttpTestingController, so the pending async
  // work in loadPage() never registers as a PendingTasks entry and whenStable() resolves
  // immediately without waiting for it. A macrotask flush is required instead -- see
  // docs/testing.md ("Waiting for async work in component tests").
  async function settle(fixture: ComponentFixture<unknown>) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function event(type: string, data: Record<string, unknown> = {}): UserEventItem {
    return { type, data: { occurredAt: '2026-01-01T00:00:00Z', ...data } };
  }

  it('shows the loading message before the first page resolves', async () => {
    const { fixture } = await setup(vi.fn(() => new Promise<UserEventsPage>(() => {})));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Loading events…');
  });

  it('requests the first page with a null cursor and the component page size on init', async () => {
    const listCurrentUserEvents = vi.fn(async () => page());
    const { fixture } = await setup(listCurrentUserEvents);
    await settle(fixture);

    expect(listCurrentUserEvents).toHaveBeenCalledTimes(1);
    expect(listCurrentUserEvents).toHaveBeenCalledWith(null, 5);
  });

  it('shows the empty state once loading finishes with no events', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('No events yet.');
    expect(compiled.textContent).not.toContain('Loading events…');
  });

  it('shows the translated error message when loading the page fails, and hides the pagination controls', async () => {
    const { fixture } = await setup(vi.fn(async () => Promise.reject(new Error('boom'))));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unable to load recent events.');
    expect(compiled.querySelector('button')).toBeNull();
  });

  it('renders the matching event-type component for each known event kind', async () => {
    const items: UserEventItem[] = [
      event('UserCreated', {
        userId: 'user-1',
        keycloakSubject: 'sub-1',
        email: { value: 'a@buddy.test', isVerified: true },
        userName: 'auser',
        name: { givenName: 'Ann', familyName: 'A' }
      }),
      event('UserDeleted', { userId: 'user-1' }),
      event('NameUpdated', {
        userId: 'user-1',
        before: { givenName: 'Ann', familyName: 'A' },
        after: { givenName: 'Anna', familyName: 'A' }
      }),
      event('EmailUpdated', {
        userId: 'user-1',
        before: { value: 'old@buddy.test', isVerified: true },
        after: { value: 'new@buddy.test', isVerified: false }
      }),
      event('EmailVerificationRequested', { userId: 'user-1', expiresAt: '2026-01-02T00:00:00Z' }),
      event('EmailVerified', { userId: 'user-1' }),
      event('TimeZoneUpdated', { userId: 'user-1', before: 'UTC', after: 'Europe/Copenhagen' }),
      event('LanguageUpdated', { userId: 'user-1', before: 'en', after: 'da' })
    ];

    const { fixture } = await setup(vi.fn(async () => page({ items })));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const selectors = [
      'app-user-created-event',
      'app-user-deleted-event',
      'app-name-updated-event',
      'app-email-updated-event',
      'app-email-verification-requested-event',
      'app-email-verified-event',
      'app-timezone-updated-event',
      'app-language-updated-event'
    ];

    for (const selector of selectors) {
      expect(compiled.querySelectorAll(selector)).toHaveLength(1);
    }
    expect(compiled.querySelector('app-unknown-event')).toBeNull();
  });

  it('falls back to the unknown-event component for an unrecognized event kind, passing through its type and data', async () => {
    const items: UserEventItem[] = [event('SomethingUnexpected', { occurredAt: '2026-01-01T00:00:00Z', foo: 'bar' })];

    const { fixture } = await setup(vi.fn(async () => page({ items })));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const unknown = compiled.querySelector('app-unknown-event');
    expect(unknown).not.toBeNull();
    expect(unknown?.textContent).toContain('SomethingUnexpected');
    expect(unknown?.textContent).toContain('"foo"');
    expect(unknown?.textContent).toContain('"bar"');
  });

  it('renders one event-type component per item, matching each item to its own kind', async () => {
    const items: UserEventItem[] = [
      event('UserCreated', {
        userId: 'user-1',
        keycloakSubject: 'sub-1',
        email: { value: 'a@buddy.test', isVerified: true },
        userName: null,
        name: { givenName: 'Ann', familyName: 'A' }
      }),
      event('UserDeleted', {}),
      event('EmailVerified', {})
    ];

    const { fixture } = await setup(vi.fn(async () => page({ items })));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const cards = compiled.querySelectorAll('.space-y-3 > div');
    expect(cards).toHaveLength(3);
    expect(cards[0].querySelector('app-user-created-event')).not.toBeNull();
    expect(cards[1].querySelector('app-user-deleted-event')).not.toBeNull();
    expect(cards[2].querySelector('app-email-verified-event')).not.toBeNull();
  });

  it('disables Previous and enables Next on the first page when a next page exists', async () => {
    const { fixture } = await setup(vi.fn(async () => page({ items: [event('UserDeleted', {})], nextCursor: 'cursor-2' })));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const [previous, next] = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(false);
  });

  it('disables Next on the first page when there is no next page', async () => {
    const { fixture } = await setup(vi.fn(async () => page({ items: [event('UserDeleted', {})], nextCursor: null })));
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const [, next] = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
    expect(next.disabled).toBe(true);
  });

  it('requests the next page using the cursor returned by the current page, and enables Previous once there', async () => {
    const listCurrentUserEvents = vi.fn(async (cursor: string | null) => {
      if (cursor === null) {
        return page({ items: [event('UserDeleted', {})], nextCursor: 'cursor-2' });
      }
      return page({ items: [event('EmailVerified', {})], nextCursor: null });
    });

    const { fixture } = await setup(listCurrentUserEvents);
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    const [, next] = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
    next.click();
    await settle(fixture);

    expect(listCurrentUserEvents).toHaveBeenNthCalledWith(2, 'cursor-2', 5);
    expect(compiled.querySelector('app-email-verified-event')).not.toBeNull();
    expect(compiled.querySelector('app-user-deleted-event')).toBeNull();

    const [previous, nextAfter] = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
    expect(previous.disabled).toBe(false);
    expect(nextAfter.disabled).toBe(true);
  });

  it('returns to the first page with a null cursor when Previous is clicked, and disables Previous again', async () => {
    const listCurrentUserEvents = vi.fn(async (cursor: string | null) => {
      if (cursor === null) {
        return page({ items: [event('UserDeleted', {})], nextCursor: 'cursor-2' });
      }
      return page({ items: [event('EmailVerified', {})], nextCursor: null });
    });

    const { fixture } = await setup(listCurrentUserEvents);
    await settle(fixture);

    const compiled = fixture.nativeElement as HTMLElement;
    (compiled.querySelectorAll('button')[1] as HTMLButtonElement).click();
    await settle(fixture);
    (compiled.querySelectorAll('button')[0] as HTMLButtonElement).click();
    await settle(fixture);

    expect(listCurrentUserEvents).toHaveBeenNthCalledWith(3, null, 5);
    expect(compiled.querySelector('app-user-deleted-event')).not.toBeNull();
    const [previous] = Array.from(compiled.querySelectorAll('button')) as HTMLButtonElement[];
    expect(previous.disabled).toBe(true);
  });

  it('reuses the cached cursor for a page already visited instead of issuing a fresh request', async () => {
    const listCurrentUserEvents = vi.fn(async (cursor: string | null) => {
      if (cursor === null) {
        return page({ items: [event('UserDeleted', {})], nextCursor: 'cursor-2' });
      }
      return page({ items: [event('EmailVerified', {})], nextCursor: null });
    });

    const { fixture } = await setup(listCurrentUserEvents);
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    (compiled.querySelectorAll('button')[1] as HTMLButtonElement).click(); // -> page 1
    await settle(fixture);
    (compiled.querySelectorAll('button')[0] as HTMLButtonElement).click(); // -> page 0
    await settle(fixture);
    (compiled.querySelectorAll('button')[1] as HTMLButtonElement).click(); // -> page 1 again
    await settle(fixture);

    expect(listCurrentUserEvents).toHaveBeenCalledTimes(4);
    // Calls 2 and 4 both request page 1, and the cursor cached from call 1's response ('cursor-2')
    // is reused verbatim rather than being recomputed or dropped.
    expect(listCurrentUserEvents).toHaveBeenNthCalledWith(2, 'cursor-2', 5);
    expect(listCurrentUserEvents).toHaveBeenNthCalledWith(4, 'cursor-2', 5);
  });

  it('shows a fresh loading state while a subsequent page is being fetched', async () => {
    let resolvePage2!: (value: UserEventsPage) => void;
    const listCurrentUserEvents = vi
      .fn()
      .mockImplementationOnce(async () => page({ items: [event('UserDeleted', {})], nextCursor: 'cursor-2' }))
      .mockImplementationOnce(() => new Promise<UserEventsPage>((resolve) => (resolvePage2 = resolve)));

    const { fixture } = await setup(listCurrentUserEvents);
    await settle(fixture);
    const compiled = fixture.nativeElement as HTMLElement;

    (compiled.querySelectorAll('button')[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Loading events…');

    resolvePage2(page({ items: [event('EmailVerified', {})], nextCursor: null }));
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Loading events…');
    expect(compiled.querySelector('app-email-verified-event')).not.toBeNull();
  });
});
