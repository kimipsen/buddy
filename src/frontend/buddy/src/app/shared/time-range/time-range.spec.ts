import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';

import { TimeRange } from './time-range';

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 0));
  fixture.detectChanges();
}

describe('TimeRange', () => {
  async function setup(start = '', end = '') {
    await TestBed.configureTestingModule({ imports: [TimeRange] }).compileComponents();

    const fixture = TestBed.createComponent(TimeRange);
    const onStart = vi.fn();
    const onEnd = vi.fn();
    fixture.componentInstance.startChange.subscribe(onStart);
    fixture.componentInstance.endChange.subscribe(onEnd);
    fixture.componentRef.setInput('start', start);
    fixture.componentRef.setInput('end', end);
    fixture.componentRef.setInput('startLabel', 'Ritual starts');
    fixture.componentRef.setInput('endLabel', 'Ritual ends');
    await settle(fixture);

    const inputs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('input'));
    return { inputs, onStart, onEnd };
  }

  it('renders a labelled start and end time', async () => {
    const { inputs } = await setup('19:30', '20:10');

    expect(inputs.map((input) => input.getAttribute('aria-label'))).toEqual([
      'Ritual starts',
      'Ritual ends',
    ]);
    expect(inputs.map((input) => input.value)).toEqual(['19:30', '20:10']);
  });

  it('emits each end separately', async () => {
    const { inputs, onStart, onEnd } = await setup();

    inputs[0].value = '19:30';
    inputs[0].dispatchEvent(new Event('input'));
    expect(onStart).toHaveBeenLastCalledWith('19:30');
    expect(onEnd).not.toHaveBeenCalled();

    inputs[1].value = '20:10';
    inputs[1].dispatchEvent(new Event('input'));
    expect(onEnd).toHaveBeenLastCalledWith('20:10');
  });

  it('moves a set end along with the start, so the window keeps its length', async () => {
    const { inputs, onStart, onEnd } = await setup('19:30', '20:10');

    inputs[0].value = '23:50';
    inputs[0].dispatchEvent(new Event('input'));

    expect(onStart).toHaveBeenLastCalledWith('23:50');
    expect(onEnd).toHaveBeenLastCalledWith('00:30');
  });

  it('changes only the end when the end changes', async () => {
    const { inputs, onStart, onEnd } = await setup('19:30', '20:10');

    inputs[1].value = '21:00';
    inputs[1].dispatchEvent(new Event('input'));

    expect(onEnd).toHaveBeenLastCalledWith('21:00');
    expect(onStart).not.toHaveBeenCalled();
  });
});
