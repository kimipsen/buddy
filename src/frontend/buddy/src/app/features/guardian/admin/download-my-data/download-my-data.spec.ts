import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { UsersService } from '../../../../core/users.service';
import { DownloadMyData } from './download-my-data';

describe('DownloadMyData', () => {
  const file = new Blob(['{}'], { type: 'application/json' });
  let createObjectURL: ReturnType<typeof vi.fn>;
  let revokeObjectURL: ReturnType<typeof vi.fn>;
  let click: ReturnType<typeof vi.spyOn>;
  let clicked: HTMLAnchorElement[];

  beforeEach(() => {
    // jsdom has no object URLs and doesn't navigate on an anchor click.
    createObjectURL = vi.fn(() => 'blob:export');
    revokeObjectURL = vi.fn();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    clicked = [];
    click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicked.push(this);
    });
  });

  afterEach(() => {
    click.mockRestore();
    Reflect.deleteProperty(URL, 'createObjectURL');
    Reflect.deleteProperty(URL, 'revokeObjectURL');
  });

  function setup(downloadPersonalData: UsersService['downloadPersonalData']) {
    const usersStub: Partial<UsersService> = { downloadPersonalData: vi.fn(downloadPersonalData) };

    TestBed.configureTestingModule({
      imports: [DownloadMyData],
      providers: [{ provide: UsersService, useValue: usersStub }],
    });

    const fixture = TestBed.createComponent(DownloadMyData);
    fixture.detectChanges();

    return { fixture, users: usersStub };
  }

  // Stubbed service calls register no PendingTasks entry; a macrotask flush drains them.
  async function settle(fixture: { detectChanges: () => void }) {
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  function button(fixture: { nativeElement: HTMLElement }): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  }

  it('explains what the download contains', () => {
    const { fixture } = setup(async () => file);
    const text = (fixture.nativeElement as HTMLElement).textContent;

    expect(text).toContain('Your data');
    expect(text).toContain('the children you guard');
    expect(button(fixture).textContent).toContain('Download my data');
  });

  it('saves the export as a dated JSON file and releases the object URL', async () => {
    const { fixture, users } = setup(async () => file);

    button(fixture).click();
    await settle(fixture);

    expect(users.downloadPersonalData).toHaveBeenCalledOnce();
    expect(createObjectURL).toHaveBeenCalledWith(file);
    expect(clicked).toHaveLength(1);
    expect(clicked[0].href).toBe('blob:export');
    expect(clicked[0].download).toMatch(/^buddy-export-\d{4}-\d{2}-\d{2}\.json$/);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:export');
    expect(button(fixture).disabled).toBe(false);
  });

  it('disables the button while the export is being prepared', async () => {
    let resolve!: (blob: Blob) => void;
    const { fixture } = setup(() => new Promise<Blob>((r) => (resolve = r)));

    button(fixture).click();
    fixture.detectChanges();

    expect(button(fixture).disabled).toBe(true);
    expect(button(fixture).textContent).toContain('Preparing download…');

    resolve(file);
    await settle(fixture);

    expect(button(fixture).disabled).toBe(false);
  });

  it('explains the limit when exports come too often', async () => {
    const { fixture } = setup(async () => {
      throw new HttpErrorResponse({ status: 429 });
    });

    button(fixture).click();
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'You can download your data once every 10 minutes.',
    );
    expect(clicked).toHaveLength(0);
  });

  it('shows a generic error when the export fails otherwise', async () => {
    const { fixture } = setup(async () => {
      throw new HttpErrorResponse({ status: 500 });
    });

    button(fixture).click();
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Unable to download your data.',
    );
  });
});
