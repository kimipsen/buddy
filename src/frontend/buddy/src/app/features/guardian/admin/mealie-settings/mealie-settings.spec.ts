import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { ChildSummary, GuardiansService } from '../../../../core/guardians.service';
import {
  MealieConnection,
  MealieConnectionView,
  MealieService,
} from '../../../../core/mealie.service';
import { MealieSettings } from './mealie-settings';

describe('MealieSettings', () => {
  function child(overrides: Partial<ChildSummary> = {}): ChildSummary {
    return {
      showVisualTimers: true,
      homeworkChunks: { focusMinutes: 15, breakMinutes: 5 },
      id: 'child-1',
      name: { givenName: 'Emil', familyName: 'Holm' },
      guardianLinkId: 'link-1',
      kind: 'Parent',
      language: 'en',
      timeZoneId: 'UTC',
      ...overrides,
    };
  }

  function connected(overrides: Partial<MealieConnection> = {}): MealieConnection {
    return {
      kind: 1,
      baseUrl: 'https://mealie.holm.example',
      householdName: 'Holm',
      mealieUserName: 'buddy-sync',
      mealieVersion: 'v3.29.0',
      tokenLast4: 'b7f2',
      connectedBy: 'guardian-1',
      connectedAt: '2026-10-19T10:00:00Z',
      ...overrides,
    };
  }

  const notConnected: MealieConnectionView = { kind: 0 };

  interface Stubs {
    guardians?: Partial<GuardiansService>;
    mealie?: Partial<MealieService>;
  }

  async function setup(stubs: Stubs = {}) {
    const guardiansStub: Partial<GuardiansService> = {
      listMyChildren: vi.fn(async () => [child()]),
      ...stubs.guardians,
    };
    const mealieStub: Partial<MealieService> = {
      getConnection: vi.fn(async () => notConnected),
      connect: vi.fn(async () => connected()),
      disconnect: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => ({
        kind: 0 as const,
        mealieVersion: 'v3.29.0',
        recipeCount: 214,
        shoppingListCount: 2,
      })),
      ...stubs.mealie,
    };

    await TestBed.configureTestingModule({
      imports: [MealieSettings],
      providers: [
        { provide: GuardiansService, useValue: guardiansStub },
        { provide: MealieService, useValue: mealieStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(MealieSettings);
    return { fixture, mealie: mealieStub, compiled: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 10; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function button(compiled: HTMLElement, text: string): HTMLButtonElement | undefined {
    return Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === text,
    );
  }

  async function type(
    fixture: { detectChanges: () => void },
    input: HTMLInputElement,
    value: string,
  ) {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    await settle(fixture);
  }

  it('asks a family without children to add one first', async () => {
    const { fixture, compiled, mealie } = await setup({
      guardians: { listMyChildren: vi.fn(async () => []) },
    });
    await settle(fixture);

    expect(compiled.textContent).toContain('Add a child first');
    expect(mealie.getConnection).not.toHaveBeenCalled();
  });

  it('shows a load error', async () => {
    const { fixture, compiled } = await setup({
      mealie: { getConnection: vi.fn(async () => Promise.reject(new Error('boom'))) },
    });
    await settle(fixture);

    expect(compiled.textContent).toContain('Couldn’t load the Mealie connection.');
  });

  it('shows the form and the help when not connected, and connects', async () => {
    const { fixture, compiled, mealie } = await setup();
    await settle(fixture);

    expect(compiled.textContent).toContain('Connect your Mealie');
    expect(compiled.textContent).toContain('without admin rights');
    expect(compiled.textContent).not.toContain('Connected');
    const connect = button(compiled, 'Connect')!;
    expect(connect.disabled).toBe(true);

    await type(
      fixture,
      compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')!,
      ' https://mealie.holm.example ',
    );
    expect(connect.disabled).toBe(true);
    await type(
      fixture,
      compiled.querySelector<HTMLInputElement>('#mealieApiToken')!,
      ' token-b7f2 ',
    );
    expect(connect.disabled).toBe(false);

    connect.click();
    await settle(fixture);

    expect(mealie.connect).toHaveBeenCalledWith(
      'child-1',
      'https://mealie.holm.example',
      'token-b7f2',
    );
    expect(compiled.textContent).toContain('Connected');
    expect(compiled.querySelector('[data-testid="mealie-server"]')?.textContent?.trim()).toBe(
      'https://mealie.holm.example',
    );
    expect(compiled.textContent).toContain('Holm · connected as buddy-sync');
    expect(compiled.textContent).toContain('••••b7f2');
    expect(compiled.textContent).toContain('v3.29.0');
    expect(compiled.querySelector('#mealieApiToken')).toBeNull();
  });

  it('shows the backend’s message under the field it rejected', async () => {
    const rejection = new HttpErrorResponse({
      status: 400,
      error: { details: { apiToken: ['This token belongs to a Mealie admin.'] } },
    });
    const { fixture, compiled } = await setup({
      mealie: { connect: vi.fn(async () => Promise.reject(rejection)) },
    });
    await settle(fixture);
    await type(
      fixture,
      compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')!,
      'https://m.example',
    );
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, 'token-1234');

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieApiTokenError')?.textContent?.trim()).toBe(
      'This token belongs to a Mealie admin.',
    );
    expect(compiled.querySelector('#mealieApiToken')?.getAttribute('aria-invalid')).toBe('true');
    expect(compiled.querySelector('#mealieBaseUrlError')).toBeNull();
    expect(compiled.textContent).not.toContain('Couldn’t connect to Mealie.');
  });

  it('shows a general error when the connect failed without a field message', async () => {
    const { fixture, compiled } = await setup({
      mealie: {
        connect: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500 }))),
      },
    });
    await settle(fixture);
    await type(
      fixture,
      compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')!,
      'https://m.example',
    );
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, 'token-1234');

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Couldn’t connect to Mealie.');
  });

  it('tests the connection and shows the counts or the failure', async () => {
    const testConnection = vi
      .fn()
      .mockResolvedValueOnce({
        kind: 0,
        mealieVersion: 'v3.29.0',
        recipeCount: 214,
        shoppingListCount: 2,
      })
      .mockResolvedValueOnce({ kind: 1, code: 'mealie_token_rejected', message: 'Token rejected.' })
      .mockRejectedValueOnce(new Error('offline'));
    const { fixture, compiled } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()), testConnection },
    });
    await settle(fixture);

    button(compiled, 'Test connection')!.click();
    await settle(fixture);
    expect(testConnection).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Connection works · 214 recipes, 2 shopping lists');

    button(compiled, 'Test connection')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Connection failed: Token rejected.');

    button(compiled, 'Test connection')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Couldn’t test the connection. Try again.');
  });

  it('replaces the token with the address prefilled, and cancels', async () => {
    const { fixture, compiled, mealie } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()) },
    });
    await settle(fixture);

    button(compiled, 'Replace token')!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Connect a different server or token');
    expect(compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')?.value).toBe(
      'https://mealie.holm.example',
    );
    expect(button(compiled, 'Replace token')).toBeUndefined();

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieBaseUrl')).toBeNull();
    expect(mealie.connect).not.toHaveBeenCalled();
  });

  it('disconnects after a confirmation', async () => {
    const { fixture, compiled, mealie } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()) },
    });
    await settle(fixture);

    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Recipe links on meals are kept.');
    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    expect(mealie.disconnect).not.toHaveBeenCalled();

    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    // The confirm button reads "Disconnect" too; it is the red one in the prompt.
    const confirm = Array.from(compiled.querySelectorAll('button')).find(
      (b) => b.textContent?.trim() === 'Disconnect' && b.classList.contains('bg-red-600'),
    )!;
    confirm.click();
    await settle(fixture);

    expect(mealie.disconnect).toHaveBeenCalledWith('child-1');
    expect(compiled.textContent).toContain('Connect your Mealie');
    expect(compiled.querySelector('[data-testid="mealie-server"]')).toBeNull();
  });

  function deferred<T>() {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>((res) => (resolve = res));
    return { promise, resolve };
  }

  async function fill(
    fixture: { detectChanges: () => void },
    compiled: HTMLElement,
    url: string,
    token: string,
  ) {
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')!, url);
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, token);
  }

  it('shows no field message for an error that isn’t a 400, even with details', async () => {
    const error = new HttpErrorResponse({
      status: 502,
      error: { details: { baseUrl: ['Not shown'] } },
    });
    const { fixture, compiled } = await setup({
      mealie: { connect: vi.fn(async () => Promise.reject(error)) },
    });
    await settle(fixture);
    await fill(fixture, compiled, 'https://m.example', 'token-1234');

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieBaseUrlError')).toBeNull();
    expect(compiled.textContent).toContain('Couldn’t connect to Mealie.');
  });

  it('treats a field with no message as no field error', async () => {
    const error = new HttpErrorResponse({ status: 400, error: { details: { baseUrl: [] } } });
    const { fixture, compiled } = await setup({
      mealie: { connect: vi.fn(async () => Promise.reject(error)) },
    });
    await settle(fixture);
    await fill(fixture, compiled, 'https://m.example', 'token-1234');

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieBaseUrlError')).toBeNull();
    expect(compiled.textContent).toContain('Couldn’t connect to Mealie.');
  });

  it('clears the previous field error when a new attempt fails differently', async () => {
    const connect = vi
      .fn()
      .mockRejectedValueOnce(
        new HttpErrorResponse({ status: 400, error: { details: { baseUrl: ['Bad address.'] } } }),
      )
      .mockRejectedValueOnce(new HttpErrorResponse({ status: 500 }));
    const { fixture, compiled } = await setup({ mealie: { connect } });
    await settle(fixture);
    await fill(fixture, compiled, 'https://m.example', 'token-1234');

    button(compiled, 'Connect')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieBaseUrlError')?.textContent?.trim()).toBe('Bad address.');

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieBaseUrlError')).toBeNull();
    expect(compiled.textContent).toContain('Couldn’t connect to Mealie.');
  });

  it('starts every token replacement clean: no token, error or old test result', async () => {
    const connect = vi
      .fn()
      .mockRejectedValue(
        new HttpErrorResponse({ status: 400, error: { details: { apiToken: ['Rejected.'] } } }),
      );
    const { fixture, compiled } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()), connect },
    });
    await settle(fixture);

    button(compiled, 'Test connection')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Connection works');

    button(compiled, 'Replace token')!.click();
    await settle(fixture);
    expect(compiled.textContent).not.toContain('Connection works');
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, 'token-9999');
    button(compiled, 'Connect')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieApiTokenError')).not.toBeNull();

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    button(compiled, 'Replace token')!.click();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('#mealieApiToken')!.value).toBe('');
    expect(compiled.querySelector('#mealieApiTokenError')).toBeNull();
    expect(compiled.textContent).not.toContain('Couldn’t connect to Mealie.');
  });

  it('forgets the general connect error on cancel', async () => {
    const { fixture, compiled } = await setup({
      mealie: {
        getConnection: vi.fn(async () => connected()),
        connect: vi.fn(async () => Promise.reject(new HttpErrorResponse({ status: 500 }))),
      },
    });
    await settle(fixture);
    button(compiled, 'Replace token')!.click();
    await settle(fixture);
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, 'token-9999');
    button(compiled, 'Connect')!.click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Couldn’t connect to Mealie.');

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    button(compiled, 'Replace token')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Couldn’t connect to Mealie.');
  });

  it('empties the token field after a successful connect', async () => {
    const { fixture, compiled } = await setup();
    await settle(fixture);
    await fill(fixture, compiled, 'https://mealie.holm.example', 'token-b7f2');
    button(compiled, 'Connect')!.click();
    await settle(fixture);

    button(compiled, 'Replace token')!.click();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('#mealieApiToken')!.value).toBe('');
  });

  it('says it is testing and hides the last result until the new one is in', async () => {
    const second = deferred<{
      kind: 0;
      mealieVersion: string;
      recipeCount: number;
      shoppingListCount: number;
    }>();
    const testConnection = vi
      .fn()
      .mockResolvedValueOnce({
        kind: 0,
        mealieVersion: 'v3.29.0',
        recipeCount: 214,
        shoppingListCount: 2,
      })
      .mockReturnValueOnce(second.promise);
    const { fixture, compiled } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()), testConnection },
    });
    await settle(fixture);
    button(compiled, 'Test connection')!.click();
    await settle(fixture);

    button(compiled, 'Test connection')!.click();
    await settle(fixture);

    expect(button(compiled, 'Testing…')!.disabled).toBe(true);
    expect(compiled.textContent).not.toContain('Connection works');

    second.resolve({ kind: 0, mealieVersion: 'v3.29.0', recipeCount: 215, shoppingListCount: 2 });
    await settle(fixture);
    expect(compiled.textContent).toContain('215 recipes');
    expect(button(compiled, 'Test connection')!.disabled).toBe(false);
  });

  it('clears a disconnect error when asked again', async () => {
    const { fixture, compiled } = await setup({
      mealie: {
        getConnection: vi.fn(async () => connected()),
        disconnect: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);
    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Disconnect' && b.classList.contains('bg-red-600'))!
      .click();
    await settle(fixture);
    expect(compiled.textContent).toContain('Couldn’t disconnect Mealie.');

    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    button(compiled, 'Disconnect')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Couldn’t disconnect Mealie.');
  });

  it('leaves no prompt or old test result behind after disconnecting and connecting again', async () => {
    const { fixture, compiled } = await setup({
      mealie: { getConnection: vi.fn(async () => connected()) },
    });
    await settle(fixture);
    button(compiled, 'Test connection')!.click();
    await settle(fixture);
    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Disconnect' && b.classList.contains('bg-red-600'))!
      .click();
    await settle(fixture);

    await fill(fixture, compiled, 'https://mealie.holm.example', 'token-b7f2');
    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.textContent).not.toContain('Recipe links on meals are kept.');
    expect(compiled.textContent).not.toContain('Connection works');
    expect(button(compiled, 'Replace token')).toBeDefined();
  });

  it('shows a disconnect error', async () => {
    const { fixture, compiled } = await setup({
      mealie: {
        getConnection: vi.fn(async () => connected()),
        disconnect: vi.fn(async () => Promise.reject(new Error('boom'))),
      },
    });
    await settle(fixture);

    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Disconnect' && b.classList.contains('bg-red-600'))!
      .click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Couldn’t disconnect Mealie.');
    expect(compiled.querySelector('[data-testid="mealie-server"]')).not.toBeNull();
  });
  async function confirmDisconnect(fixture: { detectChanges: () => void }, compiled: HTMLElement) {
    button(compiled, 'Disconnect')!.click();
    await settle(fixture);
    Array.from(compiled.querySelectorAll('button'))
      .find((b) => b.textContent?.trim() === 'Disconnect' && b.classList.contains('bg-red-600'))!
      .click();
    await settle(fixture);
  }

  it('opens the form with both fields empty', async () => {
    const { fixture, compiled } = await setup();
    await settle(fixture);

    expect(compiled.querySelector<HTMLInputElement>('#mealieBaseUrl')!.value).toBe('');
    expect(compiled.querySelector<HTMLInputElement>('#mealieApiToken')!.value).toBe('');
  });

  it('hides the last field error while a new attempt runs', async () => {
    const second = deferred<MealieConnection>();
    const connect = vi
      .fn()
      .mockRejectedValueOnce(
        new HttpErrorResponse({ status: 400, error: { details: { baseUrl: ['Bad address.'] } } }),
      )
      .mockReturnValueOnce(second.promise);
    const { fixture, compiled } = await setup({ mealie: { connect } });
    await settle(fixture);
    await fill(fixture, compiled, 'https://m.example', 'token-1234');
    button(compiled, 'Connect')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieBaseUrlError')).not.toBeNull();

    button(compiled, 'Connect')!.click();
    await settle(fixture);

    expect(compiled.querySelector('#mealieBaseUrlError')).toBeNull();
    second.resolve(connected());
    await settle(fixture);
  });

  it('doesn’t keep the old token in the form after connecting and disconnecting', async () => {
    const { fixture, compiled } = await setup();
    await settle(fixture);
    await fill(fixture, compiled, 'https://mealie.holm.example', 'token-b7f2');
    button(compiled, 'Connect')!.click();
    await settle(fixture);

    await confirmDisconnect(fixture, compiled);

    expect(compiled.querySelector<HTMLInputElement>('#mealieApiToken')!.value).toBe('');
  });

  it('shows no error from a cancelled token replacement once disconnected', async () => {
    const { fixture, compiled } = await setup({
      mealie: {
        getConnection: vi.fn(async () => connected()),
        connect: vi.fn(async () =>
          Promise.reject(
            new HttpErrorResponse({ status: 400, error: { details: { apiToken: ['Rejected.'] } } }),
          ),
        ),
      },
    });
    await settle(fixture);

    button(compiled, 'Replace token')!.click();
    await settle(fixture);
    await type(fixture, compiled.querySelector<HTMLInputElement>('#mealieApiToken')!, 'token-9999');
    button(compiled, 'Connect')!.click();
    await settle(fixture);
    expect(compiled.querySelector('#mealieApiTokenError')).not.toBeNull();
    button(compiled, 'Cancel')!.click();
    await settle(fixture);
    await confirmDisconnect(fixture, compiled);

    expect(compiled.querySelector('#mealieApiTokenError')).toBeNull();
    expect(compiled.textContent).not.toContain('Couldn’t connect to Mealie.');
  });
});
