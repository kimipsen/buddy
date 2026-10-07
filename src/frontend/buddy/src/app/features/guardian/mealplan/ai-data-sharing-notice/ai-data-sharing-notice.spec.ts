import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { AiAssistantService, AiProviderSettings } from '../../../../core/ai-assistant.service';
import { AiDataSharingNotice } from './ai-data-sharing-notice';

@Component({
  imports: [AiDataSharingNotice],
  template: `<app-ai-data-sharing-notice
    childId="child-1"
    [acknowledgedAt]="acknowledgedAt()"
    [canAcknowledge]="canAcknowledge()"
    (acknowledged)="received.push($event)"
  />`,
})
class Host {
  readonly acknowledgedAt = signal<string | null>(null);
  readonly canAcknowledge = signal(true);
  readonly received: AiProviderSettings[] = [];
}

describe('AiDataSharingNotice', () => {
  const acknowledgedSettings: AiProviderSettings = {
    providers: [],
    activeProvider: 0,
    dataSharingAcknowledgedAt: '2026-08-02T09:30:00Z',
  };

  async function setup(acknowledgeDataSharing = vi.fn(async () => acknowledgedSettings)) {
    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [{ provide: AiAssistantService, useValue: { acknowledgeDataSharing } }],
    }).compileComponents();

    const fixture = TestBed.createComponent(Host);
    return { fixture, acknowledgeDataSharing };
  }

  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();

    for (let i = 0; i < 5; i++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      fixture.detectChanges();
    }
  }

  function button(compiled: HTMLElement): HTMLButtonElement | null {
    return compiled.querySelector('button');
  }

  it('lists what is sent to the provider and how long it is kept', async () => {
    const { fixture } = await setup();
    await settle(fixture);

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain("Your family's meals, with the children's star ratings and comments.");
    expect(text).toContain('everything you write in the chat');
    expect(text).toContain('everything else is sent as "busy"');
    expect(text).toContain('Children appear as "child 1", "child 2"');
    expect(text).toContain('Buddy deletes the conversation 30 days after a session ends.');
  });

  it('acknowledges for the child and emits the updated settings', async () => {
    const { fixture, acknowledgeDataSharing } = await setup();
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(button(compiled)!.textContent!.trim()).toBe('I understand, continue');
    button(compiled)!.click();
    await settle(fixture);

    expect(acknowledgeDataSharing).toHaveBeenCalledWith('child-1');
    expect(fixture.componentInstance.received).toEqual([acknowledgedSettings]);
  });

  it('shows saving while the acknowledgement is in flight', async () => {
    let finish!: (value: AiProviderSettings) => void;
    const { fixture } = await setup(
      vi.fn(() => new Promise<AiProviderSettings>((resolve) => (finish = resolve))),
    );
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    button(compiled)!.click();
    await settle(fixture);

    expect(button(compiled)!.textContent!.trim()).toBe('Saving…');
    expect(button(compiled)!.disabled).toBe(true);

    finish(acknowledgedSettings);
    await settle(fixture);
    expect(button(compiled)!.disabled).toBe(false);
  });

  it('shows an error when the acknowledgement fails', async () => {
    const { fixture } = await setup(vi.fn(async () => Promise.reject(new Error('boom'))));
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    button(compiled)!.click();
    await settle(fixture);

    expect(compiled.textContent).toContain('Could not save your acknowledgement. Try again.');
    expect(fixture.componentInstance.received).toEqual([]);
  });

  it('shows the acknowledgement date instead of the button once acknowledged', async () => {
    const { fixture } = await setup();
    fixture.componentInstance.acknowledgedAt.set('2026-08-02T09:30:00Z');
    await settle(fixture);

    const compiled: HTMLElement = fixture.nativeElement;
    expect(compiled.textContent).toContain('Acknowledged on 2026-08-02.');
    expect(button(compiled)).toBeNull();
  });

  it('hides the button when acknowledging is not possible', async () => {
    const { fixture } = await setup();
    fixture.componentInstance.canAcknowledge.set(false);
    await settle(fixture);

    expect(button(fixture.nativeElement)).toBeNull();
  });
});
