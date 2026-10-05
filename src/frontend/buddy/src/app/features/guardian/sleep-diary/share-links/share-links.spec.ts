import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { SleepDiaryService } from '../../../../core/sleep-diary.service';
import { SleepShareLinks } from './share-links';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 2; round++) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
  fixture.detectChanges();
}

function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
  return Array.from(compiled.querySelectorAll('button')).find(
    (b) => b.textContent?.trim() === text,
  );
}

describe('SleepShareLinks', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  async function setup(service: Partial<SleepDiaryService> = {}) {
    const stub: Partial<SleepDiaryService> = {
      listShareLinks: vi.fn(async () => [
        { id: 'link-1', createdAt: '2026-03-01T10:00:00Z', expiresAt: '2026-03-31T10:00:00Z' },
        { id: 'link-2', createdAt: '2026-03-02T10:00:00Z', expiresAt: null },
      ]),
      createShareLink: vi.fn(async () => ({
        id: 'link-3',
        token: 'secret-token',
        createdAt: '2026-03-05T10:00:00Z',
        expiresAt: null,
      })),
      revokeShareLink: vi.fn(async () => undefined),
      ...service,
    };

    await TestBed.configureTestingModule({
      imports: [SleepShareLinks],
      providers: [{ provide: SleepDiaryService, useValue: stub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(SleepShareLinks);
    fixture.componentRef.setInput('childId', 'child-1');
    await settle(fixture);

    return { fixture, compiled: fixture.nativeElement as HTMLElement, stub };
  }

  it('lists the active links with their expiry', async () => {
    const { compiled, stub } = await setup();

    expect(stub.listShareLinks).toHaveBeenCalledWith('child-1');
    const items = Array.from(compiled.querySelectorAll('li')).map((li) => li.textContent);
    expect(items[0]).toContain('expires Mar 31, 2026');
    expect(items[1]).toContain('no expiry');
  });

  it('creates a link that expires after 30 days by default and shows it once', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-03-05T10:00:00Z'));
    const { fixture, compiled, stub } = await setup();

    button(compiled, 'Create link')!.click();
    await settle(fixture);

    expect(stub.createShareLink).toHaveBeenCalledWith('child-1', '2026-04-04T10:00:00.000Z');
    expect(compiled.textContent).toContain(
      `${window.location.origin}/shared/sleep-diary/secret-token`,
    );
    expect(stub.listShareLinks).toHaveBeenCalledTimes(2);
  });

  it('creates a link with no expiry when Never is chosen', async () => {
    const { fixture, compiled, stub } = await setup();

    (compiled.querySelector('[role="radio"]:last-of-type') as HTMLButtonElement).click();
    await settle(fixture);
    button(compiled, 'Create link')!.click();
    await settle(fixture);

    expect(stub.createShareLink).toHaveBeenCalledWith('child-1', null);
  });

  it('copies the new link', async () => {
    const writeText = vi.fn(async () => undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const { fixture, compiled } = await setup();

    button(compiled, 'Create link')!.click();
    await settle(fixture);
    button(compiled, 'Copy')!.click();
    await settle(fixture);

    expect(writeText).toHaveBeenCalledWith(
      `${window.location.origin}/shared/sleep-diary/secret-token`,
    );
    expect(button(compiled, 'Copied')).toBeDefined();
  });

  it('revokes a link and reloads the list', async () => {
    const { fixture, compiled, stub } = await setup();

    (compiled.querySelector('li button') as HTMLButtonElement).click();
    await settle(fixture);

    expect(stub.revokeShareLink).toHaveBeenCalledWith('child-1', 'link-1');
    expect(stub.listShareLinks).toHaveBeenCalledTimes(2);
  });

  it('shows errors from creating and loading', async () => {
    const { fixture, compiled } = await setup({
      listShareLinks: vi.fn(async () => Promise.reject(new Error('boom'))),
      createShareLink: vi.fn(async () => Promise.reject(new Error('boom'))),
    });

    expect(compiled.textContent).toContain('Unable to load the share links.');

    button(compiled, 'Create link')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Unable to create a link.');
  });
});
