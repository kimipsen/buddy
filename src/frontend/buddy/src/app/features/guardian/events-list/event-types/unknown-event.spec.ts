import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';

import { UnknownEvent } from './unknown-event';

describe('UnknownEvent', () => {
  async function setup(type: string, data: Record<string, unknown>) {
    await TestBed.configureTestingModule({ imports: [UnknownEvent] }).compileComponents();

    const fixture = TestBed.createComponent(UnknownEvent);
    fixture.componentRef.setInput('type', type);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();

    return { compiled: fixture.nativeElement as HTMLElement };
  }

  it('renders the raw, untranslated event type string and the JSON-serialized payload', async () => {
    const { compiled } = await setup('SomethingUnexpected', { foo: 'bar', count: 3 });

    expect(compiled.querySelector('p')?.textContent).toBe('SomethingUnexpected');
    expect(compiled.textContent).toContain('"foo": "bar"');
    expect(compiled.textContent).toContain('"count": 3');
  });

  it('renders nested objects, arrays, booleans, and null values within the payload verbatim', async () => {
    const { compiled } = await setup('Namespace.Weird/Type-2', {
      nested: { list: [1, 2, 3], flag: true },
      missing: null
    });

    expect(compiled.querySelector('p')?.textContent).toBe('Namespace.Weird/Type-2');
    expect(compiled.textContent).toContain('"nested"');
    expect(compiled.textContent).toContain('"list"');
    expect(compiled.textContent).toContain('"flag": true');
    expect(compiled.textContent).toContain('"missing": null');
  });
});
